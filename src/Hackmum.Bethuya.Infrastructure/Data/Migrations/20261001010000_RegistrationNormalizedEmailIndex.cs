using Hackmum.Bethuya.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations;

/// <inheritdoc />
[DbContext(typeof(BethuyaDbContext))]
[Migration("20261001010000_RegistrationNormalizedEmailIndex")]
public partial class RegistrationNormalizedEmailIndex : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE INDEX "IX_Registrations_NormalizedEmail"
            ON "Registrations" (lower("Email"));
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DROP INDEX "IX_Registrations_NormalizedEmail";
            """);
    }
}
