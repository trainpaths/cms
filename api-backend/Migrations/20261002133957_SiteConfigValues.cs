using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_backend.Migrations
{
    /// <inheritdoc />
    public partial class SiteConfigValues : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// The free-form field list becomes schema-shaped values (fixed fields + groups, see CmsConfig.SiteConfigSchema).
        /// Old fields are dropped on purpose (no production data at the time); EF's suggested rename would keep a JSON
        /// array the new shape can't read.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fields",
                table: "site_config");

            migrationBuilder.AddColumn<string>(
                name: "Values",
                table: "site_config",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Values",
                table: "site_config");

            migrationBuilder.AddColumn<string>(
                name: "Fields",
                table: "site_config",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");
        }
    }
}
