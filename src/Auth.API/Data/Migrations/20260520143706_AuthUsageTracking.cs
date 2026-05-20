using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class AuthUsageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "RefreshTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastUsedAtUtc",
                table: "ExternalLogins",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuthUsageEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientId = table.Column<string>(type: "text", nullable: true),
                    Provider = table.Column<string>(type: "text", nullable: true),
                    LoginMethod = table.Column<string>(type: "text", nullable: true),
                    RedirectUri = table.Column<string>(type: "text", nullable: true),
                    Scope = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthUsageEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthUsageEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserClientUsages",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<string>(type: "text", nullable: false),
                    FirstSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AuthorizeCount = table.Column<int>(type: "integer", nullable: false),
                    TokenExchangeCount = table.Column<int>(type: "integer", nullable: false),
                    RefreshCount = table.Column<int>(type: "integer", nullable: false),
                    LastAuthorizeAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastTokenExchangeAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRefreshAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClientUsages", x => new { x.UserId, x.ClientId });
                    table.ForeignKey(
                        name: "FK_UserClientUsages_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthUsageEvents_ClientId_CreatedAtUtc",
                table: "AuthUsageEvents",
                columns: new[] { "ClientId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthUsageEvents_CreatedAtUtc",
                table: "AuthUsageEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuthUsageEvents_EventType",
                table: "AuthUsageEvents",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_AuthUsageEvents_UserId_CreatedAtUtc",
                table: "AuthUsageEvents",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserClientUsages_ClientId",
                table: "UserClientUsages",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_UserClientUsages_LastSeenAtUtc",
                table: "UserClientUsages",
                column: "LastSeenAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthUsageEvents");

            migrationBuilder.DropTable(
                name: "UserClientUsages");

            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "LastUsedAtUtc",
                table: "ExternalLogins");
        }
    }
}
