using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RaceIQ.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeysAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_RaceResults_ActivityId",
                table: "RaceResults",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedAccounts_UserId_Provider",
                table: "ConnectedAccounts",
                columns: new[] { "UserId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisReports_ActivityId",
                table: "AnalysisReports",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_UserId_StravaActivityId",
                table: "Activities",
                columns: new[] { "UserId", "StravaActivityId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_AspNetUsers_UserId",
                table: "Activities",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AnalysisReports_Activities_ActivityId",
                table: "AnalysisReports",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ConnectedAccounts_AspNetUsers_UserId",
                table: "ConnectedAccounts",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_RaceResults_Activities_ActivityId",
                table: "RaceResults",
                column: "ActivityId",
                principalTable: "Activities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_AspNetUsers_UserId",
                table: "Activities");

            migrationBuilder.DropForeignKey(
                name: "FK_AnalysisReports_Activities_ActivityId",
                table: "AnalysisReports");

            migrationBuilder.DropForeignKey(
                name: "FK_ConnectedAccounts_AspNetUsers_UserId",
                table: "ConnectedAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_RaceResults_Activities_ActivityId",
                table: "RaceResults");

            migrationBuilder.DropIndex(
                name: "IX_RaceResults_ActivityId",
                table: "RaceResults");

            migrationBuilder.DropIndex(
                name: "IX_ConnectedAccounts_UserId_Provider",
                table: "ConnectedAccounts");

            migrationBuilder.DropIndex(
                name: "IX_AnalysisReports_ActivityId",
                table: "AnalysisReports");

            migrationBuilder.DropIndex(
                name: "IX_Activities_UserId_StravaActivityId",
                table: "Activities");
        }
    }
}
