using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FichierStocke",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NomFichierOriginal = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NomFichierStocke = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TypeContenu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TailleOctets = table.Column<long>(type: "bigint", nullable: false),
                    DateAjout = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    NombreDeTelechargements = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    IdVisibilite = table.Column<int>(type: "int", nullable: false),
                    IdUtilisateur = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FichierStocke", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FichierStocke_Utilisateur_IdUtilisateur",
                        column: x => x.IdUtilisateur,
                        principalTable: "Utilisateur",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FichierStocke_Visibilite_IdVisibilite",
                        column: x => x.IdVisibilite,
                        principalTable: "Visibilite",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FichierStocke_IdUtilisateur",
                table: "FichierStocke",
                column: "IdUtilisateur");

            migrationBuilder.CreateIndex(
                name: "IX_FichierStocke_IdVisibilite",
                table: "FichierStocke",
                column: "IdVisibilite");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FichierStocke");
        }
    }
}
