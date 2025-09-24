using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class AddFKQualiteDonnees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Donnees_QualiteDonnees_QualiteDonneesId",
                table: "Donnees");

            migrationBuilder.DropForeignKey(
                name: "FK_DonneesContexteEnvironnemental_QualiteDonnees_QualiteDonneesId",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropForeignKey(
                name: "FK_DonneesEventLogs_QualiteDonnees_QualiteDonneesId",
                table: "DonneesEventLogs");

            migrationBuilder.DropIndex(
                name: "IX_DonneesEventLogs_QualiteDonneesId",
                table: "DonneesEventLogs");

            migrationBuilder.DropIndex(
                name: "IX_DonneesContexteEnvironnemental_QualiteDonneesId",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropIndex(
                name: "IX_Donnees_QualiteDonneesId",
                table: "Donnees");

            migrationBuilder.DropColumn(
                name: "QualiteDonneesId",
                table: "DonneesEventLogs");

            migrationBuilder.DropColumn(
                name: "QualiteDonneesId",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropColumn(
                name: "QualiteDonneesId",
                table: "Donnees");

            migrationBuilder.AddColumn<int>(
                name: "IdQualiteDonnees",
                table: "DonneesEventLogs",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "IdQualiteDonnees",
                table: "DonneesContexteEnvironnemental",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "IdQualiteDonnees",
                table: "Donnees",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateIndex(
                name: "IX_DonneesEventLogs_IdQualiteDonnees",
                table: "DonneesEventLogs",
                column: "IdQualiteDonnees");

            migrationBuilder.CreateIndex(
                name: "IX_DonneesContexteEnvironnemental_IdQualiteDonnees",
                table: "DonneesContexteEnvironnemental",
                column: "IdQualiteDonnees");

            migrationBuilder.CreateIndex(
                name: "IX_Donnees_IdQualiteDonnees",
                table: "Donnees",
                column: "IdQualiteDonnees");

            migrationBuilder.AddForeignKey(
                name: "FK_Donnees_QualiteDonnees_IdQualiteDonnees",
                table: "Donnees",
                column: "IdQualiteDonnees",
                principalTable: "QualiteDonnees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DonneesContexteEnvironnemental_QualiteDonnees_IdQualiteDonnees",
                table: "DonneesContexteEnvironnemental",
                column: "IdQualiteDonnees",
                principalTable: "QualiteDonnees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DonneesEventLogs_QualiteDonnees_IdQualiteDonnees",
                table: "DonneesEventLogs",
                column: "IdQualiteDonnees",
                principalTable: "QualiteDonnees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Donnees_QualiteDonnees_IdQualiteDonnees",
                table: "Donnees");

            migrationBuilder.DropForeignKey(
                name: "FK_DonneesContexteEnvironnemental_QualiteDonnees_IdQualiteDonnees",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropForeignKey(
                name: "FK_DonneesEventLogs_QualiteDonnees_IdQualiteDonnees",
                table: "DonneesEventLogs");

            migrationBuilder.DropIndex(
                name: "IX_DonneesEventLogs_IdQualiteDonnees",
                table: "DonneesEventLogs");

            migrationBuilder.DropIndex(
                name: "IX_DonneesContexteEnvironnemental_IdQualiteDonnees",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropIndex(
                name: "IX_Donnees_IdQualiteDonnees",
                table: "Donnees");

            migrationBuilder.DropColumn(
                name: "IdQualiteDonnees",
                table: "DonneesEventLogs");

            migrationBuilder.DropColumn(
                name: "IdQualiteDonnees",
                table: "DonneesContexteEnvironnemental");

            migrationBuilder.DropColumn(
                name: "IdQualiteDonnees",
                table: "Donnees");

            migrationBuilder.AddColumn<int>(
                name: "QualiteDonneesId",
                table: "DonneesEventLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualiteDonneesId",
                table: "DonneesContexteEnvironnemental",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QualiteDonneesId",
                table: "Donnees",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonneesEventLogs_QualiteDonneesId",
                table: "DonneesEventLogs",
                column: "QualiteDonneesId");

            migrationBuilder.CreateIndex(
                name: "IX_DonneesContexteEnvironnemental_QualiteDonneesId",
                table: "DonneesContexteEnvironnemental",
                column: "QualiteDonneesId");

            migrationBuilder.CreateIndex(
                name: "IX_Donnees_QualiteDonneesId",
                table: "Donnees",
                column: "QualiteDonneesId");

            migrationBuilder.AddForeignKey(
                name: "FK_Donnees_QualiteDonnees_QualiteDonneesId",
                table: "Donnees",
                column: "QualiteDonneesId",
                principalTable: "QualiteDonnees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DonneesContexteEnvironnemental_QualiteDonnees_QualiteDonneesId",
                table: "DonneesContexteEnvironnemental",
                column: "QualiteDonneesId",
                principalTable: "QualiteDonnees",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_DonneesEventLogs_QualiteDonnees_QualiteDonneesId",
                table: "DonneesEventLogs",
                column: "QualiteDonneesId",
                principalTable: "QualiteDonnees",
                principalColumn: "Id");
        }
    }
}
