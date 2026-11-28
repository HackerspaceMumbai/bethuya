using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hackmum.Bethuya.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class MakePlannerInvocationAuditIndexUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlannerInvocationAudits_PlanningCycleId_WorkItemId",
                table: "PlannerInvocationAudits");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerInvocationAudits_PlanningCycleId_WorkItemId",
                table: "PlannerInvocationAudits",
                columns: new[] { "PlanningCycleId", "WorkItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlannerInvocationAudits_PlanningCycleId_WorkItemId",
                table: "PlannerInvocationAudits");

            migrationBuilder.CreateIndex(
                name: "IX_PlannerInvocationAudits_PlanningCycleId_WorkItemId",
                table: "PlannerInvocationAudits",
                columns: new[] { "PlanningCycleId", "WorkItemId" });
        }
    }
}
