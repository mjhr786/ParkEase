using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ParkingApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCorporateInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OwnershipType",
                table: "ParkingSpaces",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "SourceType",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "PriorityThreshold",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<int>(
                name: "MaxBookingsPerWeek",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 5);

            migrationBuilder.AlterColumn<int>(
                name: "MaxBookingsPerDay",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 1);

            migrationBuilder.AlterColumn<bool>(
                name: "AllowWeekends",
                table: "ParkingAllocations",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.CreateTable(
                name: "CorporateInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BillingTypeSnapshot = table.Column<int>(type: "integer", nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEnd = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GeneratedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PaymentNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    VoidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VoidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoidReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateInvoices_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CorporateInvoiceLineItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineType = table.Column<int>(type: "integer", nullable: false),
                    AllocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UnitAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorporateInvoiceLineItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CorporateInvoiceLineItems_CorporateInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "CorporateInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateInvoiceLineItems_AllocationId",
                table: "CorporateInvoiceLineItems",
                column: "AllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateInvoiceLineItems_BookingId",
                table: "CorporateInvoiceLineItems",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateInvoiceLineItems_InvoiceId",
                table: "CorporateInvoiceLineItems",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_CorporateInvoices_CompanyId_InvoiceNumber",
                table: "CorporateInvoices",
                columns: new[] { "CompanyId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CorporateInvoices_CompanyId_PeriodStart_PeriodEnd",
                table: "CorporateInvoices",
                columns: new[] { "CompanyId", "PeriodStart", "PeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_CorporateInvoices_CompanyId_Status_IssuedAt",
                table: "CorporateInvoices",
                columns: new[] { "CompanyId", "Status", "IssuedAt" });

            // Prevent duplicate non-void invoices for the same company period.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_CorporateInvoices_Company_Period_Active"
                ON "CorporateInvoices" ("CompanyId", "PeriodStart", "PeriodEnd")
                WHERE "Status" <> 3 AND "IsDeleted" = FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_CorporateInvoices_Company_Period_Active";""");

            migrationBuilder.DropTable(
                name: "CorporateInvoiceLineItems");

            migrationBuilder.DropTable(
                name: "CorporateInvoices");

            migrationBuilder.AlterColumn<int>(
                name: "OwnershipType",
                table: "ParkingSpaces",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "SourceType",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "PriorityThreshold",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "MaxBookingsPerWeek",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 5,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "MaxBookingsPerDay",
                table: "ParkingAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<bool>(
                name: "AllowWeekends",
                table: "ParkingAllocations",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }
    }
}
