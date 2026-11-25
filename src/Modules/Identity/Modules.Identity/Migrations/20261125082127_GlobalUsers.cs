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
                name: "App",
                schema: "identity",
                table: "UserSessions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousRefreshTokenHash",
                schema: "identity",
                table: "UserSessions",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecurityStampHash",
                schema: "identity",
                table: "UserSessions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultTenantId",
                schema: "identity",
                table: "Users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MembershipInvitations",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true),
                    TenantId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MembershipInvitations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignInSelections",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    ExpiresOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignInSelections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantMemberships",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedOnUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitedBy = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
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
                name: "IX_UserSessions_PreviousRefreshTokenHash",
                schema: "identity",
                table: "UserSessions",
                column: "PreviousRefreshTokenHash");

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
                name: "IX_MembershipInvitations_MembershipId",
                schema: "identity",
                table: "MembershipInvitations",
                column: "MembershipId");

            migrationBuilder.CreateIndex(
                name: "IX_MembershipInvitations_TokenHash",
                schema: "identity",
                table: "MembershipInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignInSelections_ExpiresOnUtc",
                schema: "identity",
                table: "SignInSelections",
                column: "ExpiresOnUtc");

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
                name: "MembershipInvitations",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "SignInSelections",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "TenantMemberships",
                schema: "identity");

            migrationBuilder.DropIndex(
                name: "IX_UserSessions_PreviousRefreshTokenHash",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "App",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "PreviousRefreshTokenHash",
                schema: "identity",
                table: "UserSessions");

            migrationBuilder.DropColumn(
                name: "SecurityStampHash",
                schema: "identity",
                table: "UserSessions");

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
