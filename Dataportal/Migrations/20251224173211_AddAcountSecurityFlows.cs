using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class AddAcountSecurityFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "MfaCodeExpiration",
                table: "Utilisateur",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MfaCodeHash",
                table: "Utilisateur",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MfaEnabled",
                table: "Utilisateur",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetTokenExpiration",
                table: "Utilisateur",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetTokenHash",
                table: "Utilisateur",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerifieLe",
                table: "DemandeDeCompte",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationToken",
                table: "DemandeDeCompte",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationTokenExpiration",
                table: "DemandeDeCompte",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MfaCodeExpiration",
                table: "Utilisateur");

            migrationBuilder.DropColumn(
                name: "MfaCodeHash",
                table: "Utilisateur");

            migrationBuilder.DropColumn(
                name: "MfaEnabled",
                table: "Utilisateur");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenExpiration",
                table: "Utilisateur");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenHash",
                table: "Utilisateur");

            migrationBuilder.DropColumn(
                name: "EmailVerifieLe",
                table: "DemandeDeCompte");

            migrationBuilder.DropColumn(
                name: "VerificationToken",
                table: "DemandeDeCompte");

            migrationBuilder.DropColumn(
                name: "VerificationTokenExpiration",
                table: "DemandeDeCompte");
        }
    }
}
