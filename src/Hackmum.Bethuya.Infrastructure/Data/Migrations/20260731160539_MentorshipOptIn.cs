using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MentorshipOptIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MentorProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ExpertiseAreas = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    IntroductionBio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AvailabilityHoursPerMonth = table.Column<int>(type: "integer", nullable: false),
                    IsDiscoverable = table.Column<bool>(type: "boolean", nullable: false),
                    OptedInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MentorProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MentorProfiles_CommunityMembers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "CommunityMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MentorProfiles_MemberId",
                table: "MentorProfiles",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MentorProfiles_Status_IsDiscoverable",
                table: "MentorProfiles",
                columns: new[] { "Status", "IsDiscoverable" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MentorProfiles");
        }
    }
}
