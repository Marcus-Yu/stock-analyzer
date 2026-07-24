using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockAnalyzer.Api.Migrations
{
    /// <inheritdoc />
    public partial class Phase2LearningSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPredefined",
                table: "LearningFactors",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LearningEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionTargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactorId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPositiveEvidence = table.Column<bool>(type: "boolean", nullable: false),
                    WeightImpact = table.Column<decimal>(type: "numeric", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LearningEvidence_LearningFactors_FactorId",
                        column: x => x.FactorId,
                        principalTable: "LearningFactors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LearningEvidence_PredictionTargets_PredictionTargetId",
                        column: x => x.PredictionTargetId,
                        principalTable: "PredictionTargets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningEvidence_FactorId",
                table: "LearningEvidence",
                column: "FactorId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningEvidence_PredictionTargetId",
                table: "LearningEvidence",
                column: "PredictionTargetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LearningEvidence");

            migrationBuilder.DropColumn(
                name: "IsPredefined",
                table: "LearningFactors");
        }
    }
}
