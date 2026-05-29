param(
    [switch]$DryRun,
    [int]$MaxRetries = 2
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = "C:\Users\phili\source\repos\QQ.Production.Intraday"
Set-Location $repoRoot

$downloader = Join-Path $repoRoot "scripts\download-polygon-fx-bbo-offline.ps1"

$packageJson = Join-Path $repoRoot "artifacts\readiness\execution-sim\phase-exec-paper-r017-if-needed-final-download-command-package.json"
$packageMd   = Join-Path $repoRoot "artifacts\readiness\execution-sim\phase-exec-paper-r017-if-needed-final-download-command-package.md"

$logDir = Join-Path $repoRoot "artifacts\readiness\execution-sim"
$logPath = Join-Path $logDir ("r017-final-missing-readiness-download-run-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss"))

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
        default { throw ("Unsupported provider symbol: {0}" -f $ProviderSymbol) }
    }
}

function Convert-UtcToSuffixTime {
    param([string]$UtcText)

    return ([DateTimeOffset]::Parse($UtcText)).UtcDateTime.ToString("yyyyMMddHHmmss")
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
        }
        else {
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
    $toUtc = $toMatch.Groups[1].Value

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
    $toParsed = [DateTimeOffset]::Parse($toUtc)

    if ($toParsed -le $fromParsed) {
        throw ("Invalid command window: FromUtc={0} ToUtc={1}" -f $fromUtc, $toUtc)
    }

    return [pscustomobject]@{
        FromUtc = $fromUtc
        ToUtc   = $toUtc
        Symbols = $symbols
        Key     = ("{0}|{1}|{2}" -f $fromUtc, $toUtc, ($symbols -join ","))
    }
}

function Find-DateRangesInText {
    param([string]$Text)

    $rangeObjects = New-Object System.Collections.Generic.List[object]

    $fromMatches = [regex]::Matches(
        $Text,
        "(FromUtc|WindowStartUtc|UtcWindowStart|UtcStart|TimeRangeStartUtc|StartUtc|TargetWindowStartUtc)`"?\s*[:=]\s*`"?(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)",
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )

    $toMatches = [regex]::Matches(
        $Text,
        "(ToUtc|WindowEndUtc|UtcWindowEnd|UtcEnd|TimeRangeEndUtc|EndUtc|TargetWindowEndUtc)`"?\s*[:=]\s*`"?(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z)",
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )

    $count = [Math]::Min($fromMatches.Count, $toMatches.Count)

    for ($i = 0; $i -lt $count; $i++) {
        $fromUtc = $fromMatches[$i].Groups[2].Value
        $toUtc = $toMatches[$i].Groups[2].Value

        if ([DateTimeOffset]::Parse($toUtc) -le [DateTimeOffset]::Parse($fromUtc)) {
            continue
        }

        $rangeObjects.Add([pscustomobject]@{
            FromUtc = $fromUtc
            ToUtc   = $toUtc
        })
    }

    return $rangeObjects
}

function Get-DownloadCommands {
    $contents = New-Object System.Collections.Generic.List[string]

    if (Test-Path $packageMd) {
        $contents.Add((Get-Content -Path $packageMd -Raw))
    }

    if (Test-Path $packageJson) {
        $contents.Add((Get-Content -Path $packageJson -Raw))
    }

    if ($contents.Count -eq 0) {
        throw ("Cannot find R017 final download package. Missing both: {0} and {1}" -f $packageJson, $packageMd)
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

    if ($commandsByKey.Count -eq 0) {
        $ranges = Find-DateRangesInText -Text $combined

        foreach ($range in $ranges) {
            $symbols = @()

            if ($globalSymbols.Count -gt 0) {
                foreach ($allowed in $allowedSymbols) {
                    if ($globalSymbols -contains $allowed) {
                        $symbols += $allowed
                    }
                }
            }
            else {
                $symbols = $allowedSymbols
            }

            if ($symbols.Count -eq 0) {
                continue
            }

            $cmd = [pscustomobject]@{
                FromUtc = $range.FromUtc
                ToUtc   = $range.ToUtc
                Symbols = $symbols
                Key     = ("{0}|{1}|{2}" -f $range.FromUtc, $range.ToUtc, ($symbols -join ","))
            }

            $commandsByKey[$cmd.Key] = $cmd
        }
    }

    $commands = @($commandsByKey.Values | Sort-Object FromUtc, ToUtc, @{ Expression = { $_.Symbols -join "," } })

    if ($commands.Count -eq 0) {
        throw @"
No download commands could be parsed from the R017 package.

Open one of these files and paste the first command block back:
$packageJson
$packageMd
"@
    }

    return $commands
}

function Get-ExpectedOutputPaths {
    param($Commands)

    $incoming = Join-Path $repoRoot "data\offline-quotes\polygon\incoming"
    $pathsByKey = @{}

    foreach ($cmd in $Commands) {
        $fromSuffix = Convert-UtcToSuffixTime -UtcText $cmd.FromUtc
        $toSuffix = Convert-UtcToSuffixTime -UtcText $cmd.ToUtc

        foreach ($symbol in $cmd.Symbols) {
            $prefix = Convert-ProviderSymbolToFilePrefix -ProviderSymbol $symbol
            $base = "{0}-{1}-{2}" -f $prefix, $fromSuffix, $toSuffix

            $quote = Join-Path $incoming ("{0}.ndjson" -f $base)
            $manifest = Join-Path $incoming ("{0}.manifest.json" -f $base)

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
    Write-Log ("Command {0} / {1}" -f $CommandIndex, $CommandCount)
    Write-Log ("FromUtc: {0}" -f $Command.FromUtc)
    Write-Log ("ToUtc:   {0}" -f $Command.ToUtc)
    Write-Log ("Symbols: {0}" -f $symbolsText)
    Write-Log "============================================================"

    if ($DryRun) {
        Write-Log "DRY RUN: not executing download."
        return
    }

    $attempt = 0

    while ($true) {
        $attempt++

        try {
            Write-Log ("Executing attempt {0}..." -f $attempt)

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
    throw ("Downloader script not found: {0}" -f $downloader)
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
"R017 final missing readiness download run" | Set-Content -Path $logPath -Encoding UTF8

$commands = Get-DownloadCommands

Write-Log ("Parsed download commands: {0}" -f $commands.Count)
Write-Log "R017 expected package says 28 templates. Parsed count may differ if windows/symbols were grouped."

$expectedPaths = Get-ExpectedOutputPaths -Commands $commands
$expectedQuotes = @($expectedPaths | Where-Object { $_.Type -eq "Quote" })
$expectedManifests = @($expectedPaths | Where-Object { $_.Type -eq "Manifest" })

Write-Log ("Expected unique quote files:    {0}" -f $expectedQuotes.Count)
Write-Log ("Expected unique manifest files: {0}" -f $expectedManifests.Count)

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
        }
        else {
            $missingManifests += $item.Path
        }
    }
}

$existingQuotes = $expectedQuotes.Count - $missingQuotes.Count
$existingManifests = $expectedManifests.Count - $missingManifests.Count

Write-Log ("Existing expected quote files:    {0} / {1}" -f $existingQuotes, $expectedQuotes.Count)
Write-Log ("Existing expected manifest files: {0} / {1}" -f $existingManifests, $expectedManifests.Count)

Write-Log ""
Write-Log ("Missing quote files: {0}" -f $missingQuotes.Count)
foreach ($path in $missingQuotes) {
    Write-Log ("MISSING QUOTE: {0}" -f $path)
}

Write-Log ""
Write-Log ("Missing manifest files: {0}" -f $missingManifests.Count)
foreach ($path in $missingManifests) {
    Write-Log ("MISSING MANIFEST: {0}" -f $path)
}

if (($missingQuotes.Count -gt 0 -or $missingManifests.Count -gt 0) -and -not $DryRun) {
    throw ("R017 downloads completed with missing expected files. See log: {0}" -f $logPath)
}

Write-Log ""
Write-Log "Done."
Write-Log ("Log file: {0}" -f $logPath)