using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QOL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListeningTriggerMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TriggerMethod",
                table: "ListeningHistory",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValueSql: "N'TAP'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TriggerMethod",
                table: "ListeningHistory");
        }
    }
}
