using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationEventEmailUniqueness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Registrations_EventId",
                table: "Registrations");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_EventId_Email",
                table: "Registrations",
                columns: new[] { "EventId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Registrations_EventId_Email",
                table: "Registrations");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_EventId",
                table: "Registrations",
                column: "EventId");
        }
    }
}
