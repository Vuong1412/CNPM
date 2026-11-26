using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QOL.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAudioTranslationsAndActionLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "AudioFiles",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Upload");

            migrationBuilder.AddColumn<Guid>(
                name: "TranslationId",
                table: "AudioFiles",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ActionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActionLogs_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LocationTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LanguageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset(7)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocationTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocationTranslations_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LocationTranslations_Locations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioFiles_TranslationId",
                table: "AudioFiles",
                column: "TranslationId");

            migrationBuilder.CreateIndex(
                name: "IX_ActionLogs_ActorUserId_OccurredAt",
                table: "ActionLogs",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LocationTranslations_LanguageId",
                table: "LocationTranslations",
                column: "LanguageId");

            migrationBuilder.CreateIndex(
                name: "IX_LocationTranslations_LocationId_LanguageId",
                table: "LocationTranslations",
                columns: new[] { "LocationId", "LanguageId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AudioFiles_LocationTranslations_TranslationId",
                table: "AudioFiles",
                column: "TranslationId",
                principalTable: "LocationTranslations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AudioFiles_LocationTranslations_TranslationId",
                table: "AudioFiles");

            migrationBuilder.DropTable(
                name: "ActionLogs");

            migrationBuilder.DropTable(
                name: "LocationTranslations");

            migrationBuilder.DropIndex(
                name: "IX_AudioFiles_TranslationId",
                table: "AudioFiles");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "AudioFiles");

            migrationBuilder.DropColumn(
                name: "TranslationId",
                table: "AudioFiles");
        }
    }
}
