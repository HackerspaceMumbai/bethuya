using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddClaimTokenToEventArchiveOutbox : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClaimToken",
            table: "EventArchiveOutboxMessages",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClaimToken",
            table: "EventArchiveOutboxMessages");
    }
}
