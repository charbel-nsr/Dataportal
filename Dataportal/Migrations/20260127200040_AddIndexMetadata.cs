using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IndexEnabled",
                table: "DonneesEventLogs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IndexError",
                table: "DonneesEventLogs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexIdColumn",
                table: "DonneesEventLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexIncludeColumn",
                table: "DonneesEventLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexName",
                table: "DonneesEventLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexStatus",
                table: "DonneesEventLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexTimeColumn",
                table: "DonneesEventLogs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexType",
                table: "DonneesEventLogs",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IndexEnabled",
                table: "DonneesContexteEnvironnemental",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IndexError",
                table: "DonneesContexteEnvironnemental",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexIdColumn",
                table: "DonneesContexteEnvironnemental",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexIncludeColumn",
                table: "DonneesContexteEnvironnemental",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexName",
                table: "DonneesContexteEnvironnemental",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexStatus",
                table: "DonneesContexteEnvironnemental",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexTimeColumn",
                table: "DonneesContexteEnvironnemental",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexType",
                table: "DonneesContexteEnvironnemental",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IndexEnabled",
                table: "Donnees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IndexError",
                table: "Donnees",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexIdColumn",
                table: "Donnees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexIncludeColumn",
                table: "Donnees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexName",
                table: "Donnees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexStatus",
                table: "Donnees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexTimeColumn",
                table: "Donnees",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IndexType",
                table: "Donnees",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IndexEnabled",
                table: "DonneesEventLogs");

            migrationBuilder.DropColumn(
                name: "IndexError",
                table: "DonneesEventLogs");

            migrationBuilder.DropColumn(
                name: "IndexIdColumn",
                table: "DonneesEventLogs");

            migrationBuilder.DropColumn(
                name: "IndexIncludeColumn",
                table: "DonneesEventLogs");

            migrationBuilder.DropColumn(
                name: "IndexName",
                table: "DonneesEventLogs");

            migrationBuilder.DropColumn(
                name: "IndexStatus",
                table: "DonneesEventLogs");

            migrationBuilder.DropColumn(
                name: "IndexTimeColumn",
                table: "DonneesEventLogs");

            migrationBuilder.DropColumn(
                name: "IndexType",
                table: "DonneesEventLogs");

            migrationBuilder.DropColumn(
                name: "IndexEnabled",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropColumn(
                name: "IndexError",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropColumn(
                name: "IndexIdColumn",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropColumn(
                name: "IndexIncludeColumn",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropColumn(
                name: "IndexName",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropColumn(
                name: "IndexStatus",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropColumn(
                name: "IndexTimeColumn",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropColumn(
                name: "IndexType",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropColumn(
                name: "IndexEnabled",
                table: "Donnees");

            migrationBuilder.DropColumn(
                name: "IndexError",
                table: "Donnees");

            migrationBuilder.DropColumn(
                name: "IndexIdColumn",
                table: "Donnees");

            migrationBuilder.DropColumn(
                name: "IndexIncludeColumn",
                table: "Donnees");

            migrationBuilder.DropColumn(
                name: "IndexName",
                table: "Donnees");

            migrationBuilder.DropColumn(
                name: "IndexStatus",
                table: "Donnees");

            migrationBuilder.DropColumn(
                name: "IndexTimeColumn",
                table: "Donnees");

            migrationBuilder.DropColumn(
                name: "IndexType",
                table: "Donnees");
        }
    }
}
