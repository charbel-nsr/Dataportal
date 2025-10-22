using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class AddEnergyType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdTypeEnergieRenouvelable",
                table: "Metadonnee",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TypeEnergieRenouvelable",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypeEnergieRenouvelable", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdTypeEnergieRenouvelable",
                table: "Metadonnee",
                column: "IdTypeEnergieRenouvelable");

            migrationBuilder.CreateIndex(
                name: "IX_TypeEnergieRenouvelable_Libelle",
                table: "TypeEnergieRenouvelable",
                column: "Libelle",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Metadonnee_TypeEnergieRenouvelable_IdTypeEnergieRenouvelable",
                table: "Metadonnee",
                column: "IdTypeEnergieRenouvelable",
                principalTable: "TypeEnergieRenouvelable",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Metadonnee_TypeEnergieRenouvelable_IdTypeEnergieRenouvelable",
                table: "Metadonnee");

            migrationBuilder.DropTable(
                name: "TypeEnergieRenouvelable");

            migrationBuilder.DropIndex(
                name: "IX_Metadonnee_IdTypeEnergieRenouvelable",
                table: "Metadonnee");

            migrationBuilder.DropColumn(
                name: "IdTypeEnergieRenouvelable",
                table: "Metadonnee");
        }
    }
}
