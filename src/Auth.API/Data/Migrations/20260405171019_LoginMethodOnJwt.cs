using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class LoginMethodOnJwt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastLoginMethod",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthMethod",
                table: "RefreshTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoginMethod",
                table: "AuthorizationCodes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLoginMethod",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AuthMethod",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "LoginMethod",
                table: "AuthorizationCodes");
        }
    }
}
