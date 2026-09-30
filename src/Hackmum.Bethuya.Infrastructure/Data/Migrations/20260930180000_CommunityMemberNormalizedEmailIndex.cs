using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations;

/// <inheritdoc />
[DbContext(typeof(BethuyaDbContext))]
[Migration("20260930180000_CommunityMemberNormalizedEmailIndex")]
public partial class CommunityMemberNormalizedEmailIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE INDEX "IX_CommunityMembers_NormalizedEmail"
            ON "CommunityMembers" (lower("Email"));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX "IX_CommunityMembers_NormalizedEmail";
            """);
    }
}
