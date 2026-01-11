using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class AddNotebookApiAccessLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "Count",
                table: "RateLimitEntries",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "NotebookApiAccessLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false),
                    IdUtilisateur = table.Column<int>(type: "int", nullable: true),
                    IdNotebookApiToken = table.Column<int>(type: "int", nullable: true),
                    AccessedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    BytesReturned = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookApiAccessLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotebookApiAccessLogs_Metadonnee_IdMetadonnee",
                        column: x => x.IdMetadonnee,
                        principalTable: "Metadonnee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotebookApiAccessLogs_NotebookApiTokens_IdNotebookApiToken",
                        column: x => x.IdNotebookApiToken,
                        principalTable: "NotebookApiTokens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NotebookApiAccessLogs_Utilisateur_IdUtilisateur",
                        column: x => x.IdUtilisateur,
                        principalTable: "Utilisateur",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookApiAccessLogs_AccessedAtUtc",
                table: "NotebookApiAccessLogs",
                column: "AccessedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookApiAccessLogs_IdMetadonnee",
                table: "NotebookApiAccessLogs",
                column: "IdMetadonnee");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookApiAccessLogs_IdNotebookApiToken",
                table: "NotebookApiAccessLogs",
                column: "IdNotebookApiToken");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookApiAccessLogs_IdUtilisateur",
                table: "NotebookApiAccessLogs",
                column: "IdUtilisateur");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotebookApiAccessLogs");

            migrationBuilder.AlterColumn<int>(
                name: "Count",
                table: "RateLimitEntries",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
