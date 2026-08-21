using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TibbiNav.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentGenerator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FileKey",
                table: "EmployeeDocuments",
                newName: "Title");

            // DocumentType переходит со свободной строки на enum (int) — таблица
            // на момент этой миграции пуста везде, кроме, возможно, dev-окружений
            // без прод-данных, поэтому drop+add вместо AlterColumn (текстовые
            // значения вроде "Contract" всё равно не castable в integer).
            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "EmployeeDocuments");

            migrationBuilder.AddColumn<int>(
                name: "DocumentType",
                table: "EmployeeDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAtUtc",
                table: "EmployeeDocuments",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedByUserId",
                table: "EmployeeDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocxFileKey",
                table: "EmployeeDocuments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "EmploymentRecordId",
                table: "EmployeeDocuments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "GeneratedAtUtc",
                table: "EmployeeDocuments",
                type: "timestamptz",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "PdfFileKey",
                table: "EmployeeDocuments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SignedAtUtc",
                table: "EmployeeDocuments",
                type: "timestamptz",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SignedByUserId",
                table: "EmployeeDocuments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "EmployeeDocuments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "DocumentTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    ModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentTemplateBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Bold = table.Column<bool>(type: "boolean", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentTemplateBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentTemplateBlocks_DocumentTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "DocumentTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeId",
                table: "EmployeeDocuments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentTemplateBlocks_TemplateId",
                table: "DocumentTemplateBlocks",
                column: "TemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentTemplateBlocks");

            migrationBuilder.DropTable(
                name: "DocumentTemplates");

            migrationBuilder.DropIndex(
                name: "IX_EmployeeDocuments_EmployeeId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "DocxFileKey",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "EmploymentRecordId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "GeneratedAtUtc",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "PdfFileKey",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "SignedAtUtc",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "SignedByUserId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "EmployeeDocuments");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "EmployeeDocuments",
                newName: "FileKey");

            migrationBuilder.AlterColumn<string>(
                name: "DocumentType",
                table: "EmployeeDocuments",
                type: "text",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
