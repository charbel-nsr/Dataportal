using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class MiseAJourDeMetadonnee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Historique");

            migrationBuilder.DropColumn(
                name: "AutoriserLesSQL",
                table: "Metadonnee");

            migrationBuilder.DropColumn(
                name: "EndTimestamp",
                table: "Metadonnee");

            migrationBuilder.DropColumn(
                name: "IdDocumentation",
                table: "Metadonnee");

            migrationBuilder.DropColumn(
                name: "StartTimestamp",
                table: "Metadonnee");

            migrationBuilder.DropColumn(
                name: "VisualiserLesdonnees",
                table: "Metadonnee");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutoriserLesSQL",
                table: "Metadonnee",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndTimestamp",
                table: "Metadonnee",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "IdDocumentation",
                table: "Metadonnee",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartTimestamp",
                table: "Metadonnee",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "VisualiserLesdonnees",
                table: "Metadonnee",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Historique",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Lien = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Historique", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Historique_Metadonnee_IdMetadonnee",
                        column: x => x.IdMetadonnee,
                        principalTable: "Metadonnee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Historique_IdMetadonnee",
                table: "Historique",
                column: "IdMetadonnee");
        }
    }
}
