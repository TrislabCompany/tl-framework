using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trislab.Modules.Identity.Migrations
{
    /// <inheritdoc />
    public partial class ImpersonationApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GrantsAdmin",
                schema: "identity",
                table: "MembershipInvitations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ImpersonationRequests",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ActorTenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TargetTenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AnsweredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GrantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpersonationRequests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationRequests_ActorUserId_RequestedAtUtc",
                schema: "identity",
                table: "ImpersonationRequests",
                columns: new[] { "ActorUserId", "RequestedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationRequests_GrantId",
                schema: "identity",
                table: "ImpersonationRequests",
                column: "GrantId");

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationRequests_TargetTenantId_TargetUserId_Requested~",
                schema: "identity",
                table: "ImpersonationRequests",
                columns: new[] { "TargetTenantId", "TargetUserId", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImpersonationRequests",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "GrantsAdmin",
                schema: "identity",
                table: "MembershipInvitations");
        }
    }
}
