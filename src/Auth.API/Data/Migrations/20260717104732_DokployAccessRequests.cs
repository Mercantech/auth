using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DokployAccessRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DokployAccessRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanCreateProjects = table.Column<bool>(type: "boolean", nullable: false),
                    CanCreateServices = table.Column<bool>(type: "boolean", nullable: false),
                    CanDeleteProjects = table.Column<bool>(type: "boolean", nullable: false),
                    CanDeleteServices = table.Column<bool>(type: "boolean", nullable: false),
                    CanAccessToDocker = table.Column<bool>(type: "boolean", nullable: false),
                    CanAccessToTraefikFiles = table.Column<bool>(type: "boolean", nullable: false),
                    CanAccessToAPI = table.Column<bool>(type: "boolean", nullable: false),
                    CanAccessToSSHKeys = table.Column<bool>(type: "boolean", nullable: false),
                    CanAccessToGitProviders = table.Column<bool>(type: "boolean", nullable: false),
                    CanDeleteEnvironments = table.Column<bool>(type: "boolean", nullable: false),
                    CanCreateEnvironments = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DokployAccessRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DokployAccessRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DokployAccessRequestProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DokployProjectId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProjectName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DokployAccessRequestProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DokployAccessRequestProjects_DokployAccessRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "DokployAccessRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DokployAccessRequestProjects_RequestId_DokployProjectId",
                table: "DokployAccessRequestProjects",
                columns: new[] { "RequestId", "DokployProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DokployAccessRequests_CreatedAtUtc",
                table: "DokployAccessRequests",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DokployAccessRequests_UserId_Status",
                table: "DokployAccessRequests",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DokployAccessRequestProjects");

            migrationBuilder.DropTable(
                name: "DokployAccessRequests");
        }
    }
}
