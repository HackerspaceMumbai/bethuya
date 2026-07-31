using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParticipationLedgerDedupeHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParticipationLedgerEntries_ProvenanceKey",
                table: "ParticipationLedgerEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipationLedgerEntries_CommunityMemberId_Connector_Prov~",
                table: "ParticipationLedgerEntries",
                columns: new[] { "CommunityMemberId", "Connector", "ProvenanceKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParticipationLedgerEntries_CommunityMemberId_Connector_Prov~",
                table: "ParticipationLedgerEntries");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipationLedgerEntries_ProvenanceKey",
                table: "ParticipationLedgerEntries",
                column: "ProvenanceKey",
                unique: true);
        }
    }
}
