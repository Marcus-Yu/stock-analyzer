using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace StockAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CachedAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Ticker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AnalysisJson = table.Column<string>(type: "text", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    VolatilityScore = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CachedAnalyses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LearningFactors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningFactors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Predictions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Ticker = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Sector = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PredictionScore = table.Column<int>(type: "integer", nullable: false),
                    Recommendation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DataConfidenceScore = table.Column<int>(type: "integer", nullable: false),
                    OriginalAnalysisJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Predictions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemSettings",
                columns: table => new
                {
                    SettingKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemSettings", x => x.SettingKey);
                });

            migrationBuilder.CreateTable(
                name: "LearningWeights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sector = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MarketRegime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Weight = table.Column<decimal>(type: "numeric", nullable: false),
                    ReliabilityScore = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningWeights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningWeights_LearningFactors_FactorId",
                        column: x => x.FactorId,
                        principalTable: "LearningFactors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PredictionSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ModelVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LearningContextUsed = table.Column<string>(type: "text", nullable: false),
                    MarketRegime = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PredictionSnapshots_Predictions_PredictionId",
                        column: x => x.PredictionId,
                        principalTable: "Predictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PredictionTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timeframe = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LowerEstimate = table.Column<decimal>(type: "numeric", nullable: false),
                    ModerateEstimate = table.Column<decimal>(type: "numeric", nullable: false),
                    HigherEstimate = table.Column<decimal>(type: "numeric", nullable: false),
                    IsEvaluated = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PredictionTargets_Predictions_PredictionId",
                        column: x => x.PredictionId,
                        principalTable: "Predictions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LearningAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningWeightId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    NewWeight = table.Column<decimal>(type: "numeric", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    AdjustedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningAdjustments_LearningWeights_LearningWeightId",
                        column: x => x.LearningWeightId,
                        principalTable: "LearningWeights",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PredictionEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActualPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    AccuracyScore = table.Column<int>(type: "integer", nullable: false),
                    EvaluationResult = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EvaluatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionEvaluations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PredictionEvaluations_PredictionTargets_PredictionTargetId",
                        column: x => x.PredictionTargetId,
                        principalTable: "PredictionTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PredictionPostMortems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    WhatWasCorrect = table.Column<string>(type: "text", nullable: false),
                    WhatWasIncorrect = table.Column<string>(type: "text", nullable: false),
                    WhatWasMissed = table.Column<string>(type: "text", nullable: false),
                    SucceededAssumptions = table.Column<string>(type: "text", nullable: false),
                    FailedAssumptions = table.Column<string>(type: "text", nullable: false),
                    FutureImprovements = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PredictionPostMortems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PredictionPostMortems_PredictionTargets_PredictionTargetId",
                        column: x => x.PredictionTargetId,
                        principalTable: "PredictionTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CachedAnalyses_Ticker_CreatedAt",
                table: "CachedAnalyses",
                columns: new[] { "Ticker", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LearningAdjustments_LearningWeightId",
                table: "LearningAdjustments",
                column: "LearningWeightId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningWeights_FactorId",
                table: "LearningWeights",
                column: "FactorId");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionEvaluations_PredictionTargetId",
                table: "PredictionEvaluations",
                column: "PredictionTargetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionPostMortems_PredictionTargetId",
                table: "PredictionPostMortems",
                column: "PredictionTargetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_Ticker",
                table: "Predictions",
                column: "Ticker");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_Timestamp",
                table: "Predictions",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionSnapshots_PredictionId",
                table: "PredictionSnapshots",
                column: "PredictionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PredictionTargets_ExpirationDate",
                table: "PredictionTargets",
                column: "ExpirationDate");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionTargets_IsEvaluated",
                table: "PredictionTargets",
                column: "IsEvaluated");

            migrationBuilder.CreateIndex(
                name: "IX_PredictionTargets_PredictionId",
                table: "PredictionTargets",
                column: "PredictionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CachedAnalyses");

            migrationBuilder.DropTable(
                name: "LearningAdjustments");

            migrationBuilder.DropTable(
                name: "PredictionEvaluations");

            migrationBuilder.DropTable(
                name: "PredictionPostMortems");

            migrationBuilder.DropTable(
                name: "PredictionSnapshots");

            migrationBuilder.DropTable(
                name: "SystemSettings");

            migrationBuilder.DropTable(
                name: "LearningWeights");

            migrationBuilder.DropTable(
                name: "PredictionTargets");

            migrationBuilder.DropTable(
                name: "LearningFactors");

            migrationBuilder.DropTable(
                name: "Predictions");
        }
    }
}
