using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QOL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVisitorSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "ListeningHistory",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "ListeningHistory",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VisitorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisitorSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VisitorSessions_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListeningHistory_SessionId",
                table: "ListeningHistory",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorSessions_DeviceId",
                table: "VisitorSessions",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisitorSessions_ExpiresAt",
                table: "VisitorSessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_VisitorSessions_LanguageId",
                table: "VisitorSessions",
                column: "LanguageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ListeningHistory_VisitorSessions_SessionId",
                table: "ListeningHistory",
                column: "SessionId",
                principalTable: "VisitorSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ListeningHistory_VisitorSessions_SessionId",
                table: "ListeningHistory");

            migrationBuilder.DropTable(
                name: "VisitorSessions");

            migrationBuilder.DropIndex(
                name: "IX_ListeningHistory_SessionId",
                table: "ListeningHistory");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "ListeningHistory");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "ListeningHistory",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);
        }
    }
}
