using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Payroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArabicNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "TaxSchemes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "PayComponentTypes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameAr",
                table: "Departments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 1,
                column: "NameAr",
                value: "الهندسة");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2,
                column: "NameAr",
                value: "المالية");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 3,
                column: "NameAr",
                value: "الموارد البشرية");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 4,
                column: "NameAr",
                value: "العمليات");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 5,
                column: "NameAr",
                value: "المبيعات");

            migrationBuilder.UpdateData(
                table: "PayComponentTypes",
                keyColumn: "Id",
                keyValue: 1,
                column: "NameAr",
                value: "بدل انتقال");

            migrationBuilder.UpdateData(
                table: "PayComponentTypes",
                keyColumn: "Id",
                keyValue: 2,
                column: "NameAr",
                value: "بدل سكن");

            migrationBuilder.UpdateData(
                table: "PayComponentTypes",
                keyColumn: "Id",
                keyValue: 3,
                column: "NameAr",
                value: "بدل وجبات");

            migrationBuilder.UpdateData(
                table: "PayComponentTypes",
                keyColumn: "Id",
                keyValue: 4,
                column: "NameAr",
                value: "سداد قرض");

            migrationBuilder.UpdateData(
                table: "PayComponentTypes",
                keyColumn: "Id",
                keyValue: 5,
                column: "NameAr",
                value: "التأمين الطبي");

            migrationBuilder.UpdateData(
                table: "TaxSchemes",
                keyColumn: "Id",
                keyValue: 1,
                column: "NameAr",
                value: "النظام القياسي");

            migrationBuilder.UpdateData(
                table: "TaxSchemes",
                keyColumn: "Id",
                keyValue: 2,
                column: "NameAr",
                value: "النسبة الثابتة");

            migrationBuilder.UpdateData(
                table: "TaxSchemes",
                keyColumn: "Id",
                keyValue: 3,
                column: "NameAr",
                value: "معفى من الضريبة");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "TaxSchemes");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "PayComponentTypes");

            migrationBuilder.DropColumn(
                name: "NameAr",
                table: "Departments");
        }
    }
}
