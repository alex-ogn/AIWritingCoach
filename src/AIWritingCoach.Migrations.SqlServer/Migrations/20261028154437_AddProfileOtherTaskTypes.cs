using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AIWritingCoach.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileOtherTaskTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OtherTaskTypes",
                table: "Profiles",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtherTaskTypes",
                table: "Profiles");
        }
    }
}
