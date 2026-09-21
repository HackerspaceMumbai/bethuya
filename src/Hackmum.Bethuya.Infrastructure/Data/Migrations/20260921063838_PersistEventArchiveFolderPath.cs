using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PersistEventArchiveFolderPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchiveFolderPath",
                table: "Events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Events"
                SET "ArchiveFolderPath" = regexp_replace(
                    "GitHubFolderUrl",
                    '^https://github\.com/[^/]+/[^/]+/tree/[^/]+/',
                    '')
                WHERE "GitHubFolderUrl" ~ '^https://github\.com/[^/]+/[^/]+/tree/[^/]+/';
                """);

            migrationBuilder.Sql(
                """
                UPDATE "Events"
                SET "ArchiveFolderPath" =
                    'events/' ||
                    to_char("StartDate" AT TIME ZONE 'Asia/Kolkata', 'YYYY') || '/' ||
                    to_char("StartDate" AT TIME ZONE 'Asia/Kolkata', 'YYYY-MM-DD') || '-' ||
                    left(
                        nullif(
                            trim(both '-' from regexp_replace(lower(btrim("Title")), '[^a-z0-9-]+', '-', 'g')),
                            ''),
                        80) ||
                    '-' || replace("Id"::text, '-', '')
                WHERE "ArchiveFolderPath" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchiveFolderPath",
                table: "Events");
        }
    }
}
