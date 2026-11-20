using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EventArchivePullRequestPublication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchiveAdoptedAt",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveAdoptedBy",
                table: "Events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveBranchName",
                table: "Events",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivePublicationIdempotencyKey",
                table: "Events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivePublicationRequestedAt",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivePublicationRequestedBy",
                table: "Events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivePublicationStatus",
                table: "Events",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "ArchivePullRequestNumber",
                table: "Events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivePullRequestUrl",
                table: "Events",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveSourceMetadataSha",
                table: "Events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveSourceReadmeSha",
                table: "Events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedBy",
                table: "EventArchiveOutboxMessages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "EventArchiveOutboxMessages",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Events_ArchiveFolderPath",
                table: "Events",
                column: "ArchiveFolderPath",
                unique: true,
                filter: "\"ArchiveFolderPath\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_ArchiveFolderPath",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchiveAdoptedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchiveAdoptedBy",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchiveBranchName",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchivePublicationIdempotencyKey",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchivePublicationRequestedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchivePublicationRequestedBy",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchivePublicationStatus",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchivePullRequestNumber",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchivePullRequestUrl",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchiveSourceMetadataSha",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ArchiveSourceReadmeSha",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ConfirmedBy",
                table: "EventArchiveOutboxMessages");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "EventArchiveOutboxMessages");
        }
    }
}
