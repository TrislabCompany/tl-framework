using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace TestHost.Migrations;

/// <summary>The one migration of the test module: the Items table, tenant-stamped like every module's.</summary>
[DbContext(typeof(TestDbContext))]
[Migration("20261124000000_Baseline")]
internal sealed class Baseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.EnsureSchema(name: "testhost");

        migrationBuilder.CreateTable(
            name: "Items",
            schema: "testhost",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                FileAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                ImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                TenantId = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_Items", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_Items_FileAssetId",
            schema: "testhost",
            table: "Items",
            column: "FileAssetId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(name: "Items", schema: "testhost");
    }
}
