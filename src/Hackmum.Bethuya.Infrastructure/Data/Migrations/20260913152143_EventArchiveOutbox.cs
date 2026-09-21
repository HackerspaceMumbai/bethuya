using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations;

    /// <inheritdoc />
    public partial class EventArchiveOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventArchiveOutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Destination = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FolderPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReadmeMarkdown = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "text", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventArchiveOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventArchiveOutboxMessages_EventId_Destination_IdempotencyK~",
                table: "EventArchiveOutboxMessages",
                columns: new[] { "EventId", "Destination", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EventArchiveOutboxMessages_ProcessedAt_AvailableAt",
                table: "EventArchiveOutboxMessages",
                columns: new[] { "ProcessedAt", "AvailableAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventArchiveOutboxMessages");
        }
}
