using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CommunityGraphVerifiedTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "ParticipationLedgerEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TargetKey",
                table: "ParticipationLedgerEntries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetKind",
                table: "ParticipationLedgerEntries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetLabel",
                table: "ParticipationLedgerEntries",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "ParticipationLedgerEntries");

            migrationBuilder.DropColumn(
                name: "TargetKey",
                table: "ParticipationLedgerEntries");

            migrationBuilder.DropColumn(
                name: "TargetKind",
                table: "ParticipationLedgerEntries");

            migrationBuilder.DropColumn(
                name: "TargetLabel",
                table: "ParticipationLedgerEntries");
        }
    }
}
