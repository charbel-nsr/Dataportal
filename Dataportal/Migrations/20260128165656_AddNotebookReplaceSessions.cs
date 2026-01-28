using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class AddNotebookReplaceSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotebookReplaceSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false),
                    Schema = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    StagingTableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OldTableName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IdUtilisateur = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CommittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookReplaceSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotebookReplaceSessions_Metadonnee_IdMetadonnee",
                        column: x => x.IdMetadonnee,
                        principalTable: "Metadonnee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NotebookReplaceSessions_Utilisateur_IdUtilisateur",
                        column: x => x.IdUtilisateur,
                        principalTable: "Utilisateur",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotebookReplaceSessions_IdMetadonnee",
                table: "NotebookReplaceSessions",
                column: "IdMetadonnee");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookReplaceSessions_IdUtilisateur",
                table: "NotebookReplaceSessions",
                column: "IdUtilisateur");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotebookReplaceSessions");
        }
    }
}
