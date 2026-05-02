param(
    [string]$EnvironmentName = "Demo",
    [switch]$AllowExternalConnections,
    [string]$Instrument = "EURUSD",
    [string]$LmaxInstrumentId = "4001",
    [string]$SlashSymbol = "EUR/USD",
    [int]$MarketDepth = 1,
    [ValidateSet("Auto", "SnapshotOnly", "SnapshotPlusUpdates")]
    [string]$RequestMode = "Auto",
    [ValidateSet("Auto", "SecurityId", "SecurityIdNoIdSource", "SecurityIdAndSymbolWithIdSource", "SecurityIdAndSymbolNoIdSource", "SlashSymbol", "InternalSymbol", "SecurityIdAndSymbol")]
    [string]$SymbolEncodingMode = "SecurityId",
    [switch]$ShowFixMessages,
    [int]$ConnectTimeoutSeconds = 10,
    [int]$LogonTimeoutSeconds = 10,
    [int]$MaxWaitSeconds = 10,
    [int]$MaxMessages = 5
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root "tools\QQ.Production.Intraday.Lmax.ConnectivityLab\QQ.Production.Intraday.Lmax.ConnectivityLab.csproj"
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_HOME = Join-Path $root ".dotnet_home"
$labArgs = @(
  "fix-marketdata-snapshot-smoke",
  "--environment=$EnvironmentName",
  "--allow-external-connections=$($AllowExternalConnections.IsPresent)",
  "--instrument=$Instrument",
  "--lmax-instrument-id=$LmaxInstrumentId",
  "--slash-symbol=$SlashSymbol",
  "--market-depth=$MarketDepth",
  "--request-mode=$RequestMode",
  "--symbol-encoding-mode=$SymbolEncodingMode",
  "--show-fix-messages=$($ShowFixMessages.IsPresent)",
  "--connect-timeout-seconds=$ConnectTimeoutSeconds",
  "--logon-timeout-seconds=$LogonTimeoutSeconds",
  "--max-wait-seconds=$MaxWaitSeconds",
  "--max-messages=$MaxMessages"
)

dotnet run --project $project --no-build --no-restore -- @labArgs
