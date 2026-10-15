using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_backend.Migrations
{
    /// <inheritdoc />
    public partial class PublishedSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasUnpublishedChanges",
                table: "pages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublishedBlocks",
                table: "pages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedMetaDescription",
                table: "pages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PublishedMetaTitle",
                table: "pages",
                type: "character varying(70)",
                maxLength: 70,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PublishedTitle",
                table: "pages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // published pages go live with what they show now: snapshot = working copy, no pending changes
            migrationBuilder.Sql("""
                UPDATE pages SET "PublishedTitle" = "Title", "PublishedMetaTitle" = "MetaTitle",
                    "PublishedMetaDescription" = "MetaDescription", "PublishedBlocks" = "Blocks"
                WHERE "Status" = 'Published';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasUnpublishedChanges",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "PublishedBlocks",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "PublishedMetaDescription",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "PublishedMetaTitle",
                table: "pages");

            migrationBuilder.DropColumn(
                name: "PublishedTitle",
                table: "pages");
        }
    }
}
