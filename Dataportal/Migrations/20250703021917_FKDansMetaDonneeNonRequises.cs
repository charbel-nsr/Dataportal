using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class FKDansMetaDonneeNonRequises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Metadonnee_DonneesContexteEnvironnemental_IdDonneesContexteEnvironnemental",
                table: "Metadonnee");

            migrationBuilder.DropForeignKey(
                name: "FK_Metadonnee_DonneesEventLogs_IdDonneesEventLogs",
                table: "Metadonnee");

            migrationBuilder.DropIndex(
                name: "IX_Metadonnee_IdDonneesContexteEnvironnemental",
                table: "Metadonnee");

            migrationBuilder.DropIndex(
                name: "IX_Metadonnee_IdDonneesEventLogs",
                table: "Metadonnee");

            migrationBuilder.AlterColumn<int>(
                name: "IdDonneesEventLogs",
                table: "Metadonnee",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "IdDonneesContexteEnvironnemental",
                table: "Metadonnee",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdDonneesContexteEnvironnemental",
                table: "Metadonnee",
                column: "IdDonneesContexteEnvironnemental",
                unique: true,
                filter: "[IdDonneesContexteEnvironnemental] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdDonneesEventLogs",
                table: "Metadonnee",
                column: "IdDonneesEventLogs",
                unique: true,
                filter: "[IdDonneesEventLogs] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Metadonnee_DonneesContexteEnvironnemental_IdDonneesContexteEnvironnemental",
                table: "Metadonnee",
                column: "IdDonneesContexteEnvironnemental",
                principalTable: "DonneesContexteEnvironnemental",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Metadonnee_DonneesEventLogs_IdDonneesEventLogs",
                table: "Metadonnee",
                column: "IdDonneesEventLogs",
                principalTable: "DonneesEventLogs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Metadonnee_DonneesContexteEnvironnemental_IdDonneesContexteEnvironnemental",
                table: "Metadonnee");

            migrationBuilder.DropForeignKey(
                name: "FK_Metadonnee_DonneesEventLogs_IdDonneesEventLogs",
                table: "Metadonnee");

            migrationBuilder.DropIndex(
                name: "IX_Metadonnee_IdDonneesContexteEnvironnemental",
                table: "Metadonnee");

            migrationBuilder.DropIndex(
                name: "IX_Metadonnee_IdDonneesEventLogs",
                table: "Metadonnee");

            migrationBuilder.AlterColumn<int>(
                name: "IdDonneesEventLogs",
                table: "Metadonnee",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "IdDonneesContexteEnvironnemental",
                table: "Metadonnee",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdDonneesContexteEnvironnemental",
                table: "Metadonnee",
                column: "IdDonneesContexteEnvironnemental",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdDonneesEventLogs",
                table: "Metadonnee",
                column: "IdDonneesEventLogs",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Metadonnee_DonneesContexteEnvironnemental_IdDonneesContexteEnvironnemental",
                table: "Metadonnee",
                column: "IdDonneesContexteEnvironnemental",
                principalTable: "DonneesContexteEnvironnemental",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Metadonnee_DonneesEventLogs_IdDonneesEventLogs",
                table: "Metadonnee",
                column: "IdDonneesEventLogs",
                principalTable: "DonneesEventLogs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
