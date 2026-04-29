using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartWorkFlowX.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOriginalAssignedToColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OriginalAssignedTo",
                table: "Tasks",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_OriginalAssignedTo",
                table: "Tasks",
                column: "OriginalAssignedTo");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_Users_OriginalAssignedTo",
                table: "Tasks",
                column: "OriginalAssignedTo",
                principalTable: "Users",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_Users_OriginalAssignedTo",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_OriginalAssignedTo",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "OriginalAssignedTo",
                table: "Tasks");
        }
    }
}
