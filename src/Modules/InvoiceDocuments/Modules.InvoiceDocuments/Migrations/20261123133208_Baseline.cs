using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trislab.Modules.InvoiceDocuments.Migrations
{
    /// <inheritdoc />
    public partial class Baseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "invoicedocuments");

            migrationBuilder.CreateTable(
                name: "Customers",
                schema: "invoicedocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    AddressLine = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TaxNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "invoicedocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SeriesKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerAddressLine = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CustomerPostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CustomerCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CustomerCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CustomerTaxNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IssuerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IssuerAddressLine = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IssuerPostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IssuerCity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IssuerCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IssuerTaxNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IssuerIban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IssueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReferenceDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertedToDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalNet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalGross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HeaderText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FooterText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IssuerProfiles",
                schema: "invoicedocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddressLine = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TaxNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Iban = table.Column<string>(type: "character varying(34)", maxLength: 34, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    DefaultCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DefaultPaymentTermDays = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IssuerProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                schema: "invoicedocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberingSeries",
                schema: "invoicedocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LastNumber = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberingSeries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentLines",
                schema: "invoicedocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TaxCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: false),
                    TaxNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Net = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Tax = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Gross = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentLines_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalSchema: "invoicedocuments",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_Name",
                schema: "invoicedocuments",
                table: "Customers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLines_DocumentId_Position",
                schema: "invoicedocuments",
                table: "DocumentLines",
                columns: new[] { "DocumentId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CustomerId",
                schema: "invoicedocuments",
                table: "Documents",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_IssueDate",
                schema: "invoicedocuments",
                table: "Documents",
                column: "IssueDate");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_ReferenceDocumentId",
                schema: "invoicedocuments",
                table: "Documents",
                column: "ReferenceDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_SeriesKey_Number",
                schema: "invoicedocuments",
                table: "Documents",
                columns: new[] { "SeriesKey", "Number", "TenantId" },
                unique: true,
                filter: "\"Number\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Status_DueDate",
                schema: "invoicedocuments",
                table: "Documents",
                columns: new[] { "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_Name",
                schema: "invoicedocuments",
                table: "Items",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_NumberingSeries_SeriesKey",
                schema: "invoicedocuments",
                table: "NumberingSeries",
                columns: new[] { "SeriesKey", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Customers",
                schema: "invoicedocuments");

            migrationBuilder.DropTable(
                name: "DocumentLines",
                schema: "invoicedocuments");

            migrationBuilder.DropTable(
                name: "IssuerProfiles",
                schema: "invoicedocuments");

            migrationBuilder.DropTable(
                name: "Items",
                schema: "invoicedocuments");

            migrationBuilder.DropTable(
                name: "NumberingSeries",
                schema: "invoicedocuments");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "invoicedocuments");
        }
    }
}
