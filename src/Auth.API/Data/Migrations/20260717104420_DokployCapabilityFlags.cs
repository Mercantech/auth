using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class DokployCapabilityFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanAccessToAPI",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessToDocker",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessToGitProviders",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessToSSHKeys",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanAccessToTraefikFiles",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanCreateEnvironments",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanCreateProjects",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanCreateServices",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanDeleteEnvironments",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanDeleteProjects",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CanDeleteServices",
                table: "DokployUserLinks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanAccessToAPI",
                table: "DokployUserLinks");

            migrationBuilder.DropColumn(
                name: "CanAccessToDocker",
                table: "DokployUserLinks");

            migrationBuilder.DropColumn(
                name: "CanAccessToGitProviders",
                table: "DokployUserLinks");

            migrationBuilder.DropColumn(
                name: "CanAccessToSSHKeys",
                table: "DokployUserLinks");

            migrationBuilder.DropColumn(
                name: "CanAccessToTraefikFiles",
                table: "DokployUserLinks");

            migrationBuilder.DropColumn(
                name: "CanCreateEnvironments",
                table: "DokployUserLinks");

            migrationBuilder.DropColumn(
                name: "CanCreateProjects",
                table: "DokployUserLinks");

            migrationBuilder.DropColumn(
                name: "CanCreateServices",
                table: "DokployUserLinks");

            migrationBuilder.DropColumn(
                name: "CanDeleteEnvironments",
                table: "DokployUserLinks");

            migrationBuilder.DropColumn(
                name: "CanDeleteProjects",
                table: "DokployUserLinks");

            migrationBuilder.DropColumn(
                name: "CanDeleteServices",
                table: "DokployUserLinks");
        }
    }
}
