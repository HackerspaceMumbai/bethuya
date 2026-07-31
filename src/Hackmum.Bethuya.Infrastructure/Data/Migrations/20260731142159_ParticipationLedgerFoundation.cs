using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ParticipationLedgerFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParticipationLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Connector = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExternalMemberKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalRecordId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Activity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    ProvenanceKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceCorrelationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IngestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipationLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParticipationLedgerEntries_CommunityMembers_CommunityMember~",
                        column: x => x.CommunityMemberId,
                        principalTable: "CommunityMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParticipationLedgerEntries_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParticipationLedgerEntries_CommunityMemberId_OccurredAt",
                table: "ParticipationLedgerEntries",
                columns: new[] { "CommunityMemberId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ParticipationLedgerEntries_EventId",
                table: "ParticipationLedgerEntries",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipationLedgerEntries_ProvenanceKey",
                table: "ParticipationLedgerEntries",
                column: "ProvenanceKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParticipationLedgerEntries");
        }
    }
}
