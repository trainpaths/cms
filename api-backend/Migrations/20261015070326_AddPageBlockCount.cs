using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace api_backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPageBlockCount : Migration
    {
        /// <summary>
        /// Sets pages."BlockCount" from the stored tree: blocks at all depths, like PageService.CountBlocks.
        /// Non-array Blocks/innerBlocks count as empty. Public so a test can run it against seeded rows.
        /// </summary>
        public const string BackfillSql = """
            WITH RECURSIVE b(page_id, block) AS (
                SELECT p."Id", e.value
                FROM pages p,
                    jsonb_array_elements(CASE WHEN jsonb_typeof(p."Blocks") = 'array' THEN p."Blocks" ELSE '[]'::jsonb END) e
                UNION ALL
                SELECT b.page_id, c.value
                FROM b,
                    jsonb_array_elements(CASE WHEN jsonb_typeof(b.block -> 'innerBlocks') = 'array'
                        THEN b.block -> 'innerBlocks' ELSE '[]'::jsonb END) c
            )
            UPDATE pages p SET "BlockCount" = n.count
            FROM (SELECT page_id, count(*)::integer AS count FROM b GROUP BY page_id) n
            WHERE p."Id" = n.page_id;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BlockCount",
                table: "pages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(BackfillSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlockCount",
                table: "pages");
        }
    }
}
