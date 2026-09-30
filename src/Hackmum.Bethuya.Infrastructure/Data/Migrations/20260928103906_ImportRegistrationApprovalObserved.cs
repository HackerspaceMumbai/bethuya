using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportRegistrationApprovalObserved : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ApprovalObservedAt",
                table: "Registrations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalObservedAt",
                table: "Registrations");
        }
    }
}
