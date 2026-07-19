using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EventLifecycleManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitHubFolderUrl",
                table: "Events",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LifecycleState",
                table: "Events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "Events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationUrl",
                table: "Events",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionizeEventId",
                table: "Events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeamsAnnouncementMessageId",
                table: "Events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssetStatus",
                table: "AgendaSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AssetsDueAt",
                table: "AgendaSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordingUrl",
                table: "AgendaSessions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledEndAt",
                table: "AgendaSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ScheduledStartAt",
                table: "AgendaSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlidesUrl",
                table: "AgendaSessions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "AgendaSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceSessionId",
                table: "AgendaSessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpeakerAvatarUrl",
                table: "AgendaSessions",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpeakerGitHubHandle",
                table: "AgendaSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpeakerTwitterHandle",
                table: "AgendaSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_LifecycleState",
                table: "Events",
                column: "LifecycleState");

            migrationBuilder.CreateIndex(
                name: "IX_Events_SessionizeEventId",
                table: "Events",
                column: "SessionizeEventId",
                unique: true,
                filter: "\"SessionizeEventId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AgendaSessions_AgendaId_Source_SourceSessionId",
                table: "AgendaSessions",
                columns: new[] { "AgendaId", "Source", "SourceSessionId" },
                unique: true,
                filter: "\"SourceSessionId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_LifecycleState",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_SessionizeEventId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_AgendaSessions_AgendaId_Source_SourceSessionId",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "GitHubFolderUrl",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "LifecycleState",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "RegistrationUrl",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "SessionizeEventId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TeamsAnnouncementMessageId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "AssetStatus",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "AssetsDueAt",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "RecordingUrl",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "ScheduledEndAt",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "ScheduledStartAt",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "SlidesUrl",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "SourceSessionId",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "SpeakerAvatarUrl",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "SpeakerGitHubHandle",
                table: "AgendaSessions");

            migrationBuilder.DropColumn(
                name: "SpeakerTwitterHandle",
                table: "AgendaSessions");
        }
    }
}
