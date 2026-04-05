using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserEmails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    NormalizedEmail = table.Column<string>(type: "text", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    LinkedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserEmails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserEmails_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserEmails_NormalizedEmail",
                table: "UserEmails",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserEmails_UserId_Kind",
                table: "UserEmails",
                columns: new[] { "UserId", "Kind" },
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO "UserEmails" ("Id", "UserId", "NormalizedEmail", "Kind", "LinkedAt")
                SELECT gen_random_uuid(), u."Id", lower(btrim(u."Email")), 0, u."CreatedAt"
                FROM "Users" u
                WHERE u."Email" IS NOT NULL AND btrim(u."Email") <> ''
                  AND NOT EXISTS (
                    SELECT 1 FROM "UserEmails" e WHERE e."NormalizedEmail" = lower(btrim(u."Email"))
                  );
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO "UserEmails" ("Id", "UserId", "NormalizedEmail", "Kind", "LinkedAt")
                SELECT gen_random_uuid(), ll."UserId", lower(btrim(ll."Email")), 0, ll."CreatedAt"
                FROM "LocalLogins" ll
                WHERE NOT EXISTS (
                  SELECT 1 FROM "UserEmails" e WHERE e."UserId" = ll."UserId" AND e."Kind" = 0
                )
                AND NOT EXISTS (
                  SELECT 1 FROM "UserEmails" e WHERE e."NormalizedEmail" = lower(btrim(ll."Email"))
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserEmails");
        }
    }
}
