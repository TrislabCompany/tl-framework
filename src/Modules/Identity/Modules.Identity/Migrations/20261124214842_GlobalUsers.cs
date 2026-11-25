using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trislab.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class GlobalUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "UserTokens");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshToken",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiryTime",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "UserLogins");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "UserClaims");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "identity",
                table: "PasswordHistory");

            migrationBuilder.AddColumn<string>(
                name: "DefaultTenantId",
                schema: "identity",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TenantMemberships",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantMemberships_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "Users",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_TenantId",
                schema: "identity",
                table: "TenantMemberships",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_UserId_TenantId",
                schema: "identity",
                table: "TenantMemberships",
                columns: new[] { "UserId", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantMemberships",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DefaultTenantId",
                schema: "identity",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "identity",
                table: "UserTokens",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "identity",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RefreshToken",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RefreshTokenExpiryTime",
                schema: "identity",
                table: "Users",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "identity",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "identity",
                table: "UserLogins",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "identity",
                table: "UserClaims",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                schema: "identity",
                table: "PasswordHistory",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "Users",
                columns: new[] { "NormalizedUserName", "TenantId" },
                unique: true);
        }
    }
}
