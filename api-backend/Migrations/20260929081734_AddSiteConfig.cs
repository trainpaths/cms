using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddSiteConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "site_config",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Fields = table.Column<string>(type: "jsonb", nullable: false),
                    LogoMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    IconMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_config", x => x.Id);
                    table.ForeignKey(
                        name: "FK_site_config_media_assets_IconMediaId",
                        column: x => x.IconMediaId,
                        principalTable: "media_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_site_config_media_assets_LogoMediaId",
                        column: x => x.LogoMediaId,
                        principalTable: "media_assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_site_config_staff_UpdatedById",
                        column: x => x.UpdatedById,
                        principalTable: "staff",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_site_config_IconMediaId",
                table: "site_config",
                column: "IconMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_site_config_LogoMediaId",
                table: "site_config",
                column: "LogoMediaId");

            migrationBuilder.CreateIndex(
                name: "IX_site_config_UpdatedById",
                table: "site_config",
                column: "UpdatedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "site_config");
        }
    }
}
