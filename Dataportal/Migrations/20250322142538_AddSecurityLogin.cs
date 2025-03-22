using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityLogin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Metadonnee_utilisateur_IdUtilisateur",
                table: "Metadonnee");

            migrationBuilder.DropForeignKey(
                name: "FK_utilisateur_Entreprise_IdEntreprise",
                table: "utilisateur");

            migrationBuilder.DropForeignKey(
                name: "FK_utilisateur_Role_IdRole",
                table: "utilisateur");

            migrationBuilder.DropPrimaryKey(
                name: "PK_utilisateur",
                table: "utilisateur");

            migrationBuilder.RenameTable(
                name: "utilisateur",
                newName: "Utilisateur");

            migrationBuilder.RenameIndex(
                name: "IX_utilisateur_IdRole",
                table: "Utilisateur",
                newName: "IX_Utilisateur_IdRole");

            migrationBuilder.RenameIndex(
                name: "IX_utilisateur_IdEntreprise",
                table: "Utilisateur",
                newName: "IX_Utilisateur_IdEntreprise");

            migrationBuilder.RenameIndex(
                name: "IX_utilisateur_Email",
                table: "Utilisateur",
                newName: "IX_Utilisateur_Email");

            migrationBuilder.AddColumn<DateTime>(
                name: "FinLockout",
                table: "Utilisateur",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NbrEchecsAcces",
                table: "Utilisateur",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Utilisateur",
                table: "Utilisateur",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Metadonnee_Utilisateur_IdUtilisateur",
                table: "Metadonnee",
                column: "IdUtilisateur",
                principalTable: "Utilisateur",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Utilisateur_Entreprise_IdEntreprise",
                table: "Utilisateur",
                column: "IdEntreprise",
                principalTable: "Entreprise",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Utilisateur_Role_IdRole",
                table: "Utilisateur",
                column: "IdRole",
                principalTable: "Role",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Metadonnee_Utilisateur_IdUtilisateur",
                table: "Metadonnee");

            migrationBuilder.DropForeignKey(
                name: "FK_Utilisateur_Entreprise_IdEntreprise",
                table: "Utilisateur");

            migrationBuilder.DropForeignKey(
                name: "FK_Utilisateur_Role_IdRole",
                table: "Utilisateur");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Utilisateur",
                table: "Utilisateur");

            migrationBuilder.DropColumn(
                name: "FinLockout",
                table: "Utilisateur");

            migrationBuilder.DropColumn(
                name: "NbrEchecsAcces",
                table: "Utilisateur");

            migrationBuilder.RenameTable(
                name: "Utilisateur",
                newName: "utilisateur");

            migrationBuilder.RenameIndex(
                name: "IX_Utilisateur_IdRole",
                table: "utilisateur",
                newName: "IX_utilisateur_IdRole");

            migrationBuilder.RenameIndex(
                name: "IX_Utilisateur_IdEntreprise",
                table: "utilisateur",
                newName: "IX_utilisateur_IdEntreprise");

            migrationBuilder.RenameIndex(
                name: "IX_Utilisateur_Email",
                table: "utilisateur",
                newName: "IX_utilisateur_Email");

            migrationBuilder.AddPrimaryKey(
                name: "PK_utilisateur",
                table: "utilisateur",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Metadonnee_utilisateur_IdUtilisateur",
                table: "Metadonnee",
                column: "IdUtilisateur",
                principalTable: "utilisateur",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_utilisateur_Entreprise_IdEntreprise",
                table: "utilisateur",
                column: "IdEntreprise",
                principalTable: "Entreprise",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_utilisateur_Role_IdRole",
                table: "utilisateur",
                column: "IdRole",
                principalTable: "Role",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
