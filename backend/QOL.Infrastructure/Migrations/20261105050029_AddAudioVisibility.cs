using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QOL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AudioFiles",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AudioFiles");
        }
    }
}
