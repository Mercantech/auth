using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExternalOAuthTokensCipher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalOAuthTokensCipher",
                table: "RefreshTokens",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalOAuthTokensCipher",
                table: "AuthorizationCodes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalOAuthTokensCipher",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ExternalOAuthTokensCipher",
                table: "AuthorizationCodes");
        }
    }
}
