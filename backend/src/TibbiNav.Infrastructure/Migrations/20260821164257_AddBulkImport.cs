using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TibbiNav.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBulkImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClinicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceHeadersJson = table.Column<string>(type: "text", nullable: false),
                    RawRowsJson = table.Column<string>(type: "text", nullable: false),
                    ColumnMappingJson = table.Column<string>(type: "text", nullable: true),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    ValidRows = table.Column<int>(type: "integer", nullable: false),
                    ErrorRows = table.Column<int>(type: "integer", nullable: false),
                    ValidationResultJson = table.Column<string>(type: "text", nullable: true),
                    CreatedCount = table.Column<int>(type: "integer", nullable: false),
                    UpdatedCount = table.Column<int>(type: "integer", nullable: false),
                    SkippedCount = table.Column<int>(type: "integer", nullable: false),
                    ImportResultJson = table.Column<string>(type: "text", nullable: true),
                    ValidatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportBatches");
        }
    }
}
