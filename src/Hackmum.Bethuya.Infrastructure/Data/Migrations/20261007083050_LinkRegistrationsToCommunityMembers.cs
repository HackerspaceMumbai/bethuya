using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class LinkRegistrationsToCommunityMembers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "CommunityMemberId",
            table: "Registrations",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Registrations_CommunityMemberId",
            table: "Registrations",
            column: "CommunityMemberId");

        migrationBuilder.AddForeignKey(
            name: "FK_Registrations_CommunityMembers_CommunityMemberId",
            table: "Registrations",
            column: "CommunityMemberId",
            principalTable: "CommunityMembers",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Registrations_CommunityMembers_CommunityMemberId",
            table: "Registrations");

        migrationBuilder.DropIndex(
            name: "IX_Registrations_CommunityMemberId",
            table: "Registrations");

        migrationBuilder.DropColumn(
            name: "CommunityMemberId",
            table: "Registrations");
    }
}
