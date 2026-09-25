using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImportBatchId",
                table: "ParticipationLedgerEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IngestionMethod",
                table: "ParticipationLedgerEntries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "ApiOrWebhook");

            migrationBuilder.CreateTable(
                name: "ImportTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ImportKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClonedFromTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ImportTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ValidRows = table.Column<int>(type: "integer", nullable: false),
                    ErrorRows = table.Column<int>(type: "integer", nullable: false),
                    RowsToCreate = table.Column<int>(type: "integer", nullable: false),
                    RowsToUpdate = table.Column<int>(type: "integer", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DryRunCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportBatches_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ImportBatches_ImportTemplates_ImportTemplateId",
                        column: x => x.ImportTemplateId,
                        principalTable: "ImportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ImportColumnMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceColumnName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TargetField = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportColumnMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportColumnMappings_ImportTemplates_ImportTemplateId",
                        column: x => x.ImportTemplateId,
                        principalTable: "ImportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Checksum = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportArtifacts_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportRawRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowIndex = table.Column<int>(type: "integer", nullable: false),
                    RawDataJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRawRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportRawRows_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportArtifacts_ImportBatchId",
                table: "ImportArtifacts",
                column: "ImportBatchId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_EventId_CreatedAt",
                table: "ImportBatches",
                columns: new[] { "EventId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_ImportTemplateId",
                table: "ImportBatches",
                column: "ImportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportColumnMappings_ImportTemplateId",
                table: "ImportColumnMappings",
                column: "ImportTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRawRows_ImportBatchId_RowIndex",
                table: "ImportRawRows",
                columns: new[] { "ImportBatchId", "RowIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParticipationLedgerEntries_ImportBatchId",
                table: "ParticipationLedgerEntries",
                column: "ImportBatchId");

            migrationBuilder.AddForeignKey(
                name: "FK_ParticipationLedgerEntries_ImportBatches_ImportBatchId",
                table: "ParticipationLedgerEntries",
                column: "ImportBatchId",
                principalTable: "ImportBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.CreateIndex(
                name: "IX_ImportTemplates_Scope_OwnerUserId",
                table: "ImportTemplates",
                columns: new[] { "Scope", "OwnerUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ParticipationLedgerEntries_ImportBatches_ImportBatchId",
                table: "ParticipationLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_ParticipationLedgerEntries_ImportBatchId",
                table: "ParticipationLedgerEntries");

            migrationBuilder.DropTable(
                name: "ImportArtifacts");

            migrationBuilder.DropTable(
                name: "ImportColumnMappings");

            migrationBuilder.DropTable(
                name: "ImportRawRows");

            migrationBuilder.DropTable(
                name: "ImportBatches");

            migrationBuilder.DropTable(
                name: "ImportTemplates");

            migrationBuilder.DropColumn(
                name: "ImportBatchId",
                table: "ParticipationLedgerEntries");

            migrationBuilder.DropColumn(
                name: "IngestionMethod",
                table: "ParticipationLedgerEntries");
        }
    }
}
