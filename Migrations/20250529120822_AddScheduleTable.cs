using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace dotnetprojects.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOnly",
                table: "CountData",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<TimeSpan>(
                name: "TimeOnly",
                table: "CountData",
                type: "time",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            migrationBuilder.AddColumn<string>(
                name: "CameraAPIURL",
                table: "Cameras",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    ScheduleID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CameraID = table.Column<int>(type: "int", nullable: false),
                    ScheduleName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationInSec = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.ScheduleID);
                    table.ForeignKey(
                        name: "FK_Schedules_Cameras_CameraID",
                        column: x => x.CameraID,
                        principalTable: "Cameras",
                        principalColumn: "CameraID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Cameras",
                keyColumn: "CameraID",
                keyValue: 1,
                column: "CameraAPIURL",
                value: null);

            migrationBuilder.UpdateData(
                table: "Cameras",
                keyColumn: "CameraID",
                keyValue: 2,
                column: "CameraAPIURL",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_CameraID",
                table: "Schedules",
                column: "CameraID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropColumn(
                name: "DateOnly",
                table: "CountData");

            migrationBuilder.DropColumn(
                name: "TimeOnly",
                table: "CountData");

            migrationBuilder.DropColumn(
                name: "CameraAPIURL",
                table: "Cameras");
        }
    }
}
