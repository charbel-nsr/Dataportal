using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class AddQualiteDonnees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "QualiteDonnees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualiteDonnees", x => x.Id);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_QualiteDonnees_Libelle",
                table: "QualiteDonnees",
                column: "Libelle",
                unique: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropTable(
                name: "QualiteDonnees");

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
        }
    }
}
