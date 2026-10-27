using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_backend.Migrations
{
    /// <inheritdoc />
    public partial class RenderedPageLang : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // not in backups; everything re-renders on start (renderer version check)
            migrationBuilder.Sql("DELETE FROM rendered_pages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_rendered_pages",
                table: "rendered_pages");

            migrationBuilder.AddColumn<string>(
                name: "Lang",
                table: "rendered_pages",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_rendered_pages",
                table: "rendered_pages",
                columns: new[] { "PageId", "Lang" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM rendered_pages");

            migrationBuilder.DropPrimaryKey(
                name: "PK_rendered_pages",
                table: "rendered_pages");

            migrationBuilder.DropColumn(
                name: "Lang",
                table: "rendered_pages");

            migrationBuilder.AddPrimaryKey(
                name: "PK_rendered_pages",
                table: "rendered_pages",
                column: "PageId");
        }
    }
}
