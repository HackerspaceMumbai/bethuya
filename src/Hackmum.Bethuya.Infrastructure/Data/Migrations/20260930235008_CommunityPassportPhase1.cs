using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CommunityPassportPhase1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ClaimToken",
                table: "EventArchiveOutboxMessages",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<bool>(
                name: "AppearInCollaboratorDiscovery",
                table: "CommunityMembers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AppearInMentorshipRecommendations",
                table: "CommunityMembers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AppearInOpportunityRecommendations",
                table: "CommunityMembers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AppearInSpeakerRecommendations",
                table: "CommunityMembers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "AppearInVolunteerLeadershipRecommendations",
                table: "CommunityMembers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableRelationshipInsights",
                table: "CommunityMembers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReceiveOpportunityRecommendations",
                table: "CommunityMembers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                """
                UPDATE "CommunityMembers"
                SET "AppearInCollaboratorDiscovery" = "IsDiscoverableToCommunity",
                    "AppearInMentorshipRecommendations" = "IsDiscoverableToCommunity",
                    "AppearInOpportunityRecommendations" = "IsDiscoverableToCommunity",
                    "AppearInSpeakerRecommendations" = "IsDiscoverableToCommunity",
                    "AppearInVolunteerLeadershipRecommendations" = "IsDiscoverableToCommunity";
                """);

            migrationBuilder.CreateTable(
                name: "CommunityPortfolioEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    LinksJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    EvidenceEntryIdsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityPortfolioEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityPortfolioEntries_CommunityMembers_CommunityMemberId",
                        column: x => x.CommunityMemberId,
                        principalTable: "CommunityMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommunityRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Context = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EvidenceEntryIdsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    EstablishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityRelationships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunityRelationships_CommunityMembers_SourceMemberId",
                        column: x => x.SourceMemberId,
                        principalTable: "CommunityMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CommunityRelationships_CommunityMembers_TargetMemberId",
                        column: x => x.TargetMemberId,
                        principalTable: "CommunityMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CommunitySignalAwards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    EvidenceEntryIdsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    AwardedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AwardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RevocationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunitySignalAwards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommunitySignalAwards_CommunityMembers_CommunityMemberId",
                        column: x => x.CommunityMemberId,
                        principalTable: "CommunityMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberOpportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CurrentStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LifecycleJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    OfferedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberOpportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberOpportunities_CommunityMembers_CommunityMemberId",
                        column: x => x.CommunityMemberId,
                        principalTable: "CommunityMembers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityPortfolioEntries_CommunityMemberId_DisplayOrder",
                table: "CommunityPortfolioEntries",
                columns: new[] { "CommunityMemberId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityRelationships_SourceMemberId_TargetMemberId_Kind",
                table: "CommunityRelationships",
                columns: new[] { "SourceMemberId", "TargetMemberId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommunityRelationships_TargetMemberId",
                table: "CommunityRelationships",
                column: "TargetMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_CommunitySignalAwards_CommunityMemberId_Kind",
                table: "CommunitySignalAwards",
                columns: new[] { "CommunityMemberId", "Kind" },
                unique: true,
                filter: "\"RevokedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MemberOpportunities_CommunityMemberId_OfferedAt",
                table: "MemberOpportunities",
                columns: new[] { "CommunityMemberId", "OfferedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityPortfolioEntries");

            migrationBuilder.DropTable(
                name: "CommunityRelationships");

            migrationBuilder.DropTable(
                name: "CommunitySignalAwards");

            migrationBuilder.DropTable(
                name: "MemberOpportunities");

            migrationBuilder.AlterColumn<string>(
                name: "ClaimToken",
                table: "EventArchiveOutboxMessages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.DropColumn(
                name: "AppearInCollaboratorDiscovery",
                table: "CommunityMembers");

            migrationBuilder.DropColumn(
                name: "AppearInMentorshipRecommendations",
                table: "CommunityMembers");

            migrationBuilder.DropColumn(
                name: "AppearInOpportunityRecommendations",
                table: "CommunityMembers");

            migrationBuilder.DropColumn(
                name: "AppearInSpeakerRecommendations",
                table: "CommunityMembers");

            migrationBuilder.DropColumn(
                name: "AppearInVolunteerLeadershipRecommendations",
                table: "CommunityMembers");

            migrationBuilder.DropColumn(
                name: "EnableRelationshipInsights",
                table: "CommunityMembers");

            migrationBuilder.DropColumn(
                name: "ReceiveOpportunityRecommendations",
                table: "CommunityMembers");
        }
    }
}
