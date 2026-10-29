using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIWritingCoach.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddVocabularySource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "VocabularyItems",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Manual");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "VocabularyItems");
        }
    }
}
