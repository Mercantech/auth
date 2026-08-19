using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class ClientAppTokenLifetimes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccessTokenExpiryMinutes",
                table: "ClientApps",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefreshTokenExpiryDays",
                table: "ClientApps",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessTokenExpiryMinutes",
                table: "ClientApps");

            migrationBuilder.DropColumn(
                name: "RefreshTokenExpiryDays",
                table: "ClientApps");
        }
    }
}
