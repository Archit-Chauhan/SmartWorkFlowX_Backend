using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SmartWorkFlowX.Infrastructure.Data;

#nullable disable

namespace SmartWorkFlowX.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(SmartWorkflowXDbContext))]
    [Migration("20260608120000_AddTaskAssignedRole")]
    public partial class AddTaskAssignedRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedRoleId",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Tasks",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssignedRoleId",
                table: "Tasks",
                column: "AssignedRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Roles_AssignedRoleId",
                table: "Tasks",
                column: "AssignedRoleId",
                principalTable: "Roles",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.NoAction);

            // Backfill: tasks currently sitting at an approval step (step >= 1) become role-pool tasks,
            // so a previously pinned (possibly absent) approver no longer blocks them.
            migrationBuilder.Sql(@"
                UPDATE t
                SET t.AssignedRoleId = s.ApproverRoleId, t.AssignedTo = NULL
                FROM Tasks t
                INNER JOIN WorkflowSteps s ON s.WorkflowId = t.WorkflowId AND s.StepOrder = t.CurrentStepOrder
                WHERE t.CurrentStepOrder >= 1 AND t.Status = 'In Progress' AND t.IsDeleted = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Roles_AssignedRoleId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_AssignedRoleId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "AssignedRoleId",
                table: "Tasks");
        }
    }
}
