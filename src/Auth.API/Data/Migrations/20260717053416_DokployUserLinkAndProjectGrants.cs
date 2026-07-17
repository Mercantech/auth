using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DokployUserLinkAndProjectGrants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DokployProjectGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DokployProjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    GrantedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DokployProjectGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DokployProjectGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DokployUserLinks",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DokployUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LinkedEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    ProvisionedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsProvisioned = table.Column<bool>(type: "boolean", nullable: false),
                    AclDirty = table.Column<bool>(type: "boolean", nullable: false),
                    AclSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DokployUserLinks", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_DokployUserLinks_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DokployProjectGrants_UserId_DokployProjectId",
                table: "DokployProjectGrants",
                columns: new[] { "UserId", "DokployProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DokployUserLinks_DokployUserId",
                table: "DokployUserLinks",
                column: "DokployUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DokployUserLinks_LinkedEmail",
                table: "DokployUserLinks",
                column: "LinkedEmail");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DokployProjectGrants");

            migrationBuilder.DropTable(
                name: "DokployUserLinks");
        }
    }
}
