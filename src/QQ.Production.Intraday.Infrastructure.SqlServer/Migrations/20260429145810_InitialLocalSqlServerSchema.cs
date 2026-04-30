using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QQ.Production.Intraday.Infrastructure.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class InitialLocalSqlServerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BarBuildRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timeframe = table.Column<int>(type: "int", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BuilderVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BarsCreated = table.Column<int>(type: "int", nullable: false),
                    BarsUpdated = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BarBuildRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Funds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Funds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetClass = table.Column<int>(type: "int", nullable: false),
                    BaseCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    QuoteCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PricePrecision = table.Column<int>(type: "int", nullable: false),
                    QuantityPrecision = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KillSwitchStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KillSwitchStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskLimitSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskLimits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VenueType = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BrokerAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BrokerAccounts_Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModelRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AsOfUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EffectiveAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FrequencyMinutes = table.Column<int>(type: "int", nullable: false),
                    NavUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InputHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsProcessed = table.Column<bool>(type: "bit", nullable: false),
                    TargetQuantityMode = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ModelRuns_Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NavSnapshots",
                columns: table => new
                {
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AsOfUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NavUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NavSnapshots", x => new { x.FundId, x.AsOfUtc });
                    table.ForeignKey(
                        name: "FK_NavSnapshots_Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskLimitSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GlobalTradingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    MaxGrossExposureUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    MaxModelRunAge = table.Column<long>(type: "bigint", nullable: false),
                    MaxMarketDataAge = table.Column<long>(type: "bigint", nullable: false),
                    PositionToleranceBaseQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    MinDriftVenueQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskLimitSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskLimitSets_Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradingWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false),
                    OpensAtUtc = table.Column<TimeOnly>(type: "time", nullable: false),
                    ClosesAtUtc = table.Column<TimeOnly>(type: "time", nullable: false),
                    NoNewOrdersAfterUtc = table.Column<TimeOnly>(type: "time", nullable: false),
                    FlattenAtUtc = table.Column<TimeOnly>(type: "time", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TradingEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradingWindows_Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InstrumentRiskLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskLimitSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaxTradeNotionalUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    MaxExposureUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstrumentRiskLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstrumentRiskLimits_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PositionLedgerEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    BaseQuantityDelta = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    ReferenceId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PositionLedgerEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PositionLedgerEvents_Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PositionLedgerEvents_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDataBars",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Timeframe = table.Column<int>(type: "int", nullable: false),
                    BarStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BarEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BidOpen = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    BidHigh = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    BidLow = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    BidClose = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    AskOpen = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    AskHigh = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    AskLow = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    AskClose = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    MidOpen = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    MidHigh = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    MidLow = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    MidClose = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    SpreadOpen = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    SpreadHigh = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    SpreadLow = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    SpreadClose = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    SpreadAverage = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    ObservationCount = table.Column<int>(type: "int", nullable: false),
                    FirstSnapshotUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSnapshotUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsComplete = table.Column<bool>(type: "bit", nullable: false),
                    QualityStatus = table.Column<int>(type: "int", nullable: false),
                    BuildRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BuilderVersion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataBars", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketDataBars_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketDataBars_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MarketDataSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Bid = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Ask = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    ExplicitMid = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceTimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: true),
                    IsSynthetic = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketDataSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketDataSnapshots_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MarketDataSnapshots_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VenueInstrumentMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueSymbol = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VenueInstrumentCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContractSize = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    MinOrderQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    QuantityStep = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    PriceTickSize = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueInstrumentMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenueInstrumentMappings_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VenueInstrumentMappings_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VenueRiskLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RiskLimitSetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaxTradeNotionalUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenueRiskLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenueRiskLimits_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DriftSnapshots",
                columns: table => new
                {
                    ModelRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetBaseQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    CurrentBaseQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    DriftBaseQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    TargetVenueQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    CurrentVenueQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    DriftVenueQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriftSnapshots", x => new { x.ModelRunId, x.InstrumentId });
                    table.ForeignKey(
                        name: "FK_DriftSnapshots_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DriftSnapshots_ModelRuns_ModelRunId",
                        column: x => x.ModelRunId,
                        principalTable: "ModelRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HasBlockingBreaks = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationRuns_ModelRuns_ModelRunId",
                        column: x => x.ModelRunId,
                        principalTable: "ModelRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TargetPositions",
                columns: table => new
                {
                    ModelRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetNotionalUsd = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    TargetBaseQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    TargetVenueQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    TargetQuantityMode = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetPositions", x => new { x.ModelRunId, x.InstrumentId });
                    table.ForeignKey(
                        name: "FK_TargetPositions_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TargetPositions_ModelRuns_ModelRunId",
                        column: x => x.ModelRunId,
                        principalTable: "ModelRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TargetWeights",
                columns: table => new
                {
                    ModelRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    RawSecurityId = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TargetWeights", x => new { x.ModelRunId, x.InstrumentId });
                    table.ForeignKey(
                        name: "FK_TargetWeights_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TargetWeights_ModelRuns_ModelRunId",
                        column: x => x.ModelRunId,
                        principalTable: "ModelRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradeIntents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    RequestedBaseQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    RequestedVenueQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeIntents_Funds_FundId",
                        column: x => x.FundId,
                        principalTable: "Funds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeIntents_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeIntents_ModelRuns_ModelRunId",
                        column: x => x.ModelRunId,
                        principalTable: "ModelRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationBreaks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationBreaks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationBreaks_ReconciliationRuns_ReconciliationRunId",
                        column: x => x.ReconciliationRunId,
                        principalTable: "ReconciliationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ParentOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradeIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOrderId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Algo = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentOrders_TradeIntents_TradeIntentId",
                        column: x => x.TradeIntentId,
                        principalTable: "TradeIntents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RiskDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradeIntentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RejectReason = table.Column<int>(type: "int", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RiskDecisions_TradeIntents_TradeIntentId",
                        column: x => x.TradeIntentId,
                        principalTable: "TradeIntents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChildOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ParentOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientOrderId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    OrderType = table.Column<int>(type: "int", nullable: false),
                    TimeInForce = table.Column<int>(type: "int", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    VenueQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildOrders_ParentOrders_ParentOrderId",
                        column: x => x.ParentOrderId,
                        principalTable: "ParentOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChildOrders_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExecutionReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChildOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerOrderId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BrokerExecutionId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientOrderId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExecutionReportType = table.Column<int>(type: "int", nullable: false),
                    LastQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    LastPrice = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    LeavesQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    CumulativeQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    AveragePrice = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionReports_ChildOrders_ChildOrderId",
                        column: x => x.ChildOrderId,
                        principalTable: "ChildOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecutionReports_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Fills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BrokerExecutionId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChildOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstrumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Side = table.Column<int>(type: "int", nullable: false),
                    BaseQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    VenueQuantity = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    Price = table.Column<decimal>(type: "decimal(28,10)", precision: 28, scale: 10, nullable: false),
                    TradeDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fills_ChildOrders_ChildOrderId",
                        column: x => x.ChildOrderId,
                        principalTable: "ChildOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fills_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fills_Venues_VenueId",
                        column: x => x.VenueId,
                        principalTable: "Venues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BrokerAccounts_FundId",
                table: "BrokerAccounts",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildOrders_ClientOrderId",
                table: "ChildOrders",
                column: "ClientOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChildOrders_ParentOrderId",
                table: "ChildOrders",
                column: "ParentOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildOrders_VenueId",
                table: "ChildOrders",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_DriftSnapshots_InstrumentId",
                table: "DriftSnapshots",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionReports_ChildOrderId_ReceivedAtUtc",
                table: "ExecutionReports",
                columns: new[] { "ChildOrderId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionReports_VenueId",
                table: "ExecutionReports",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_Fills_ChildOrderId",
                table: "Fills",
                column: "ChildOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Fills_InstrumentId",
                table: "Fills",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Fills_VenueId_BrokerExecutionId",
                table: "Fills",
                columns: new[] { "VenueId", "BrokerExecutionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstrumentRiskLimits_InstrumentId",
                table: "InstrumentRiskLimits",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataBars_InstrumentId_VenueId_Timeframe_BarStartUtc",
                table: "MarketDataBars",
                columns: new[] { "InstrumentId", "VenueId", "Timeframe", "BarStartUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataBars_VenueId",
                table: "MarketDataBars",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataSnapshots_InstrumentId_VenueId_ReceivedAtUtc",
                table: "MarketDataSnapshots",
                columns: new[] { "InstrumentId", "VenueId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketDataSnapshots_VenueId",
                table: "MarketDataSnapshots",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_ModelRuns_FundId_IsProcessed_ReceivedAtUtc",
                table: "ModelRuns",
                columns: new[] { "FundId", "IsProcessed", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ModelRuns_Id",
                table: "ModelRuns",
                column: "Id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentOrders_ClientOrderId",
                table: "ParentOrders",
                column: "ClientOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentOrders_TradeIntentId",
                table: "ParentOrders",
                column: "TradeIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_PositionLedgerEvents_FundId_InstrumentId_CreatedAtUtc",
                table: "PositionLedgerEvents",
                columns: new[] { "FundId", "InstrumentId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PositionLedgerEvents_InstrumentId",
                table: "PositionLedgerEvents",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationBreaks_ReconciliationRunId_Severity_Status",
                table: "ReconciliationBreaks",
                columns: new[] { "ReconciliationRunId", "Severity", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationRuns_ModelRunId",
                table: "ReconciliationRuns",
                column: "ModelRunId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskDecisions_TradeIntentId",
                table: "RiskDecisions",
                column: "TradeIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_RiskLimitSets_FundId",
                table: "RiskLimitSets",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetPositions_InstrumentId",
                table: "TargetPositions",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TargetWeights_InstrumentId",
                table: "TargetWeights",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeIntents_FundId",
                table: "TradeIntents",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeIntents_InstrumentId",
                table: "TradeIntents",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeIntents_ModelRunId_InstrumentId",
                table: "TradeIntents",
                columns: new[] { "ModelRunId", "InstrumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_TradingWindows_FundId",
                table: "TradingWindows",
                column: "FundId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueInstrumentMappings_InstrumentId",
                table: "VenueInstrumentMappings",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueInstrumentMappings_VenueId",
                table: "VenueInstrumentMappings",
                column: "VenueId");

            migrationBuilder.CreateIndex(
                name: "IX_VenueRiskLimits_VenueId",
                table: "VenueRiskLimits",
                column: "VenueId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BarBuildRuns");

            migrationBuilder.DropTable(
                name: "BrokerAccounts");

            migrationBuilder.DropTable(
                name: "DriftSnapshots");

            migrationBuilder.DropTable(
                name: "ExecutionReports");

            migrationBuilder.DropTable(
                name: "Fills");

            migrationBuilder.DropTable(
                name: "InstrumentRiskLimits");

            migrationBuilder.DropTable(
                name: "KillSwitchStates");

            migrationBuilder.DropTable(
                name: "MarketDataBars");

            migrationBuilder.DropTable(
                name: "MarketDataSnapshots");

            migrationBuilder.DropTable(
                name: "NavSnapshots");

            migrationBuilder.DropTable(
                name: "PositionLedgerEvents");

            migrationBuilder.DropTable(
                name: "ReconciliationBreaks");

            migrationBuilder.DropTable(
                name: "RiskDecisions");

            migrationBuilder.DropTable(
                name: "RiskLimits");

            migrationBuilder.DropTable(
                name: "RiskLimitSets");

            migrationBuilder.DropTable(
                name: "TargetPositions");

            migrationBuilder.DropTable(
                name: "TargetWeights");

            migrationBuilder.DropTable(
                name: "TradingWindows");

            migrationBuilder.DropTable(
                name: "VenueInstrumentMappings");

            migrationBuilder.DropTable(
                name: "VenueRiskLimits");

            migrationBuilder.DropTable(
                name: "ChildOrders");

            migrationBuilder.DropTable(
                name: "ReconciliationRuns");

            migrationBuilder.DropTable(
                name: "ParentOrders");

            migrationBuilder.DropTable(
                name: "Venues");

            migrationBuilder.DropTable(
                name: "TradeIntents");

            migrationBuilder.DropTable(
                name: "Instruments");

            migrationBuilder.DropTable(
                name: "ModelRuns");

            migrationBuilder.DropTable(
                name: "Funds");
        }
    }
}
