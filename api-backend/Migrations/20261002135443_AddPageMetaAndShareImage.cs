using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPageMetaAndShareImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ShareImageMediaId",
                table: "site_config",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "pages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "pages",
                type: "character varying(70)",
                maxLength: 70,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_site_config_ShareImageMediaId",
                table: "site_config",
                column: "ShareImageMediaId");

            migrationBuilder.AddForeignKey(
                name: "FK_site_config_media_assets_ShareImageMediaId",
                table: "site_config",
                column: "ShareImageMediaId",
                principalTable: "media_assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_site_config_media_assets_ShareImageMediaId",
                table: "site_config");

            migrationBuilder.DropIndex(
                name: "IX_site_config_ShareImageMediaId",
                table: "site_config");

            migrationBuilder.DropColumn(
                name: "ShareImageMediaId",
                table: "site_config");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                table: "pages");
        }
    }
}
