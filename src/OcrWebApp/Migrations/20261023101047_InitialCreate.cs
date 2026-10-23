using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcrWebApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    TotalPages = table.Column<int>(type: "int", nullable: false),
                    ProcessedPages = table.Column<int>(type: "int", nullable: false),
                    AverageConfidence = table.Column<float>(type: "real", nullable: true),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PreprocessGrayscale = table.Column<bool>(type: "bit", nullable: false),
                    PreprocessContrast = table.Column<bool>(type: "bit", nullable: false),
                    PreprocessDeskew = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PageNumber = table.Column<int>(type: "int", nullable: false),
                    PageImagePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ExtractedText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<float>(type: "real", nullable: true),
                    IsTextLayerDirect = table.Column<bool>(type: "bit", nullable: false),
                    BoundingBoxesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobPages_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobPages_JobId_PageNumber",
                table: "JobPages",
                columns: new[] { "JobId", "PageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_CreatedAtUtc",
                table: "Jobs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_SessionToken",
                table: "Jobs",
                column: "SessionToken");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status",
                table: "Jobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobPages");

            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}
