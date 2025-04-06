using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionProfil",
                table: "Utilisateur",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LienLinkedIn",
                table: "Utilisateur",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionProfil",
                table: "Utilisateur");

            migrationBuilder.DropColumn(
                name: "LienLinkedIn",
                table: "Utilisateur");
        }
    }
}
