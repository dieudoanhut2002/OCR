using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OcrWebApp.Migrations
{
    /// <inheritdoc />
    public partial class AddPageRangeToOcrJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PageRange",
                table: "Jobs",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PageRange",
                table: "Jobs");
        }
    }
}
