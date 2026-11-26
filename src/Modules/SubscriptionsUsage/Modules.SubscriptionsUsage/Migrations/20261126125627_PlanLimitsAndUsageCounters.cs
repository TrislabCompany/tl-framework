using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trislab.Modules.SubscriptionsUsage.Migrations;

/// <inheritdoc />
public partial class PlanLimitsAndUsageCounters : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Limits",
            schema: "billing",
            table: "Plans",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb");

        migrationBuilder.CreateTable(
            name: "UsageCounters",
            schema: "billing",
            columns: table => new
            {
                TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                MeterKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                Used = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UsageCounters", x => new { x.TenantId, x.MeterKey, x.PeriodStart });
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "UsageCounters",
            schema: "billing");

        migrationBuilder.DropColumn(
            name: "Limits",
            schema: "billing",
            table: "Plans");
    }
}
