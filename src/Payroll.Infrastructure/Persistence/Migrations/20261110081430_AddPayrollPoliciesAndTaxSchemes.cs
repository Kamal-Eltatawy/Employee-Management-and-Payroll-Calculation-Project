using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Payroll.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPayrollPoliciesAndTaxSchemes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollPolicies_EffectiveFrom",
                table: "PayrollPolicies");

            migrationBuilder.AddColumn<int>(
                name: "TaxSchemeId",
                table: "PayrollPolicies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TaxSchemeId",
                table: "Employees",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "TaxSchemes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxSchemes", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "PayrollPolicies",
                keyColumn: "Id",
                keyValue: 1,
                column: "TaxSchemeId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "PayrollPolicies",
                keyColumn: "Id",
                keyValue: 2,
                column: "TaxSchemeId",
                value: 1);

            migrationBuilder.InsertData(
                table: "TaxSchemes",
                columns: new[] { "Id", "Method", "Name" },
                values: new object[,]
                {
                    { 1, 1, "Standard" },
                    { 2, 2, "Flat Rate" },
                    { 3, 3, "Tax Exempt" }
                });

            migrationBuilder.InsertData(
                table: "PayrollPolicies",
                columns: new[] { "Id", "CreatedAtUtc", "EffectiveFrom", "InsuranceCap", "InsuranceRatePercent", "TaxSchemeId" },
                values: new object[,]
                {
                    { 3, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2025, 1, 1), 1500m, 11m, 2 },
                    { 4, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2025, 1, 1), 1500m, 11m, 3 }
                });

            migrationBuilder.InsertData(
                table: "TaxBrackets",
                columns: new[] { "Id", "PayrollPolicyId", "RatePercent", "SortOrder", "UpperBound" },
                values: new object[] { 7, 3, 15m, 1, null });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPolicies_TaxSchemeId_EffectiveFrom",
                table: "PayrollPolicies",
                columns: new[] { "TaxSchemeId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employees_TaxSchemeId",
                table: "Employees",
                column: "TaxSchemeId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxSchemes_Name",
                table: "TaxSchemes",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_TaxSchemes_TaxSchemeId",
                table: "Employees",
                column: "TaxSchemeId",
                principalTable: "TaxSchemes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollPolicies_TaxSchemes_TaxSchemeId",
                table: "PayrollPolicies",
                column: "TaxSchemeId",
                principalTable: "TaxSchemes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_TaxSchemes_TaxSchemeId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollPolicies_TaxSchemes_TaxSchemeId",
                table: "PayrollPolicies");

            migrationBuilder.DropTable(
                name: "TaxSchemes");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPolicies_TaxSchemeId_EffectiveFrom",
                table: "PayrollPolicies");

            migrationBuilder.DropIndex(
                name: "IX_Employees_TaxSchemeId",
                table: "Employees");

            migrationBuilder.DeleteData(
                table: "PayrollPolicies",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "TaxBrackets",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "PayrollPolicies",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "TaxSchemeId",
                table: "PayrollPolicies");

            migrationBuilder.DropColumn(
                name: "TaxSchemeId",
                table: "Employees");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPolicies_EffectiveFrom",
                table: "PayrollPolicies",
                column: "EffectiveFrom",
                unique: true);
        }
    }
}
