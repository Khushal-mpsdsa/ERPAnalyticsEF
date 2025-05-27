using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace dotnetprojects.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshPropertiesToCamera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cameras",
                columns: table => new
                {
                    CameraID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CameraName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RefreshRateInSeconds = table.Column<int>(type: "int", nullable: false),
                    LastRefreshTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cameras", x => x.CameraID);
                });

            migrationBuilder.CreateTable(
                name: "CountData",
                columns: table => new
                {
                    SrNo = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CameraId = table.Column<int>(type: "int", nullable: false),
                    CameraName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    In = table.Column<int>(type: "int", nullable: false),
                    Out = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<long>(type: "bigint", nullable: false),
                    EndTime = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CountData", x => x.SrNo);
                });

            migrationBuilder.InsertData(
                table: "Cameras",
                columns: new[] { "CameraID", "CameraName", "LastRefreshTimestamp", "RefreshRateInSeconds" },
                values: new object[,]
                {
                    { 1, "Kitchen Main Camera", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 0 },
                    { 2, "Swadishta Camera", new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), 0 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cameras");

            migrationBuilder.DropTable(
                name: "CountData");
        }
    }
}
