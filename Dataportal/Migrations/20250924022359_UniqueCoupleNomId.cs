using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class UniqueCoupleNomId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DonneesEventLogs_Code",
                table: "DonneesEventLogs");

            migrationBuilder.DropIndex(
                name: "IX_DonneesEventLogs_Libelle",
                table: "DonneesEventLogs");

            migrationBuilder.DropIndex(
                name: "IX_DonneesContexteEnvironnemental_Code",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropIndex(
                name: "IX_DonneesContexteEnvironnemental_Libelle",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropIndex(
                name: "IX_Donnees_Code",
                table: "Donnees");

            migrationBuilder.DropIndex(
                name: "IX_Donnees_Libelle",
                table: "Donnees");

            migrationBuilder.CreateIndex(
                name: "IX_DonneesEventLogs_Libelle_Code",
                table: "DonneesEventLogs",
                columns: new[] { "Libelle", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonneesContexteEnvironnemental_Libelle_Code",
                table: "DonneesContexteEnvironnemental",
                columns: new[] { "Libelle", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donnees_Libelle_Code",
                table: "Donnees",
                columns: new[] { "Libelle", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DonneesEventLogs_Libelle_Code",
                table: "DonneesEventLogs");

            migrationBuilder.DropIndex(
                name: "IX_DonneesContexteEnvironnemental_Libelle_Code",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropIndex(
                name: "IX_Donnees_Libelle_Code",
                table: "Donnees");

            migrationBuilder.CreateIndex(
                name: "IX_DonneesEventLogs_Code",
                table: "DonneesEventLogs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonneesEventLogs_Libelle",
                table: "DonneesEventLogs",
                column: "Libelle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonneesContexteEnvironnemental_Code",
                table: "DonneesContexteEnvironnemental",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonneesContexteEnvironnemental_Libelle",
                table: "DonneesContexteEnvironnemental",
                column: "Libelle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donnees_Code",
                table: "Donnees",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Donnees_Libelle",
                table: "Donnees",
                column: "Libelle",
                unique: true);
        }
    }
}
