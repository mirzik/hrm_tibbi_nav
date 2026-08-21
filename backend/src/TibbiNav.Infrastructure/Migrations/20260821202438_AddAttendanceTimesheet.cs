using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TibbiNav.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceTimesheet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DepartmentLeaveThresholds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaxConcurrentAbsencePercent = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DepartmentLeaveThresholds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimesheetClosures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: true),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LockedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetClosures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimesheetEditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimesheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimesheetDayId = table.Column<Guid>(type: "uuid", nullable: false),
                    EditedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    EditedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    OldValueJson = table.Column<string>(type: "text", nullable: true),
                    NewValueJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetEditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Timesheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Timesheets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimesheetDays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TimesheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DayType = table.Column<int>(type: "integer", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric", nullable: false),
                    OvertimeHours = table.Column<decimal>(type: "numeric", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimesheetDays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimesheetDays_Timesheets_TimesheetId",
                        column: x => x.TimesheetId,
                        principalTable: "Timesheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DepartmentLeaveThresholds_DepartmentId",
                table: "DepartmentLeaveThresholds",
                column: "DepartmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetClosures_DepartmentId_PeriodStart_PeriodEnd",
                table: "TimesheetClosures",
                columns: new[] { "DepartmentId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_TimesheetDays_TimesheetId",
                table: "TimesheetDays",
                column: "TimesheetId");

            migrationBuilder.CreateIndex(
                name: "IX_Timesheets_EmployeeId_PeriodStart_PeriodEnd",
                table: "Timesheets",
                columns: new[] { "EmployeeId", "PeriodStart", "PeriodEnd" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DepartmentLeaveThresholds");

            migrationBuilder.DropTable(
                name: "TimesheetClosures");

            migrationBuilder.DropTable(
                name: "TimesheetDays");

            migrationBuilder.DropTable(
                name: "TimesheetEditLogs");

            migrationBuilder.DropTable(
                name: "Timesheets");
        }
    }
}
