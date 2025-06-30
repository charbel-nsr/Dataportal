using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDocumentationAndSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Metadonnee_Documentation_IdDocumentation",
                table: "Metadonnee");

            migrationBuilder.DropTable(
                name: "Documentation");

            migrationBuilder.DropTable(
                name: "Schema_Metadonnee");

            migrationBuilder.DropTable(
                name: "Schema");

            migrationBuilder.DropIndex(
                name: "IX_Metadonnee_IdDocumentation",
                table: "Metadonnee");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Documentation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Lien = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documentation_Metadonnee_IdMetadonnee",
                        column: x => x.IdMetadonnee,
                        principalTable: "Metadonnee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Schema",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schema", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Schema_Metadonnee",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false),
                    IdSchema = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schema_Metadonnee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Schema_Metadonnee_Metadonnee_IdMetadonnee",
                        column: x => x.IdMetadonnee,
                        principalTable: "Metadonnee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Schema_Metadonnee_Schema_IdSchema",
                        column: x => x.IdSchema,
                        principalTable: "Schema",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdDocumentation",
                table: "Metadonnee",
                column: "IdDocumentation");

            migrationBuilder.CreateIndex(
                name: "IX_Documentation_IdMetadonnee",
                table: "Documentation",
                column: "IdMetadonnee");

            migrationBuilder.CreateIndex(
                name: "IX_Schema_Libelle",
                table: "Schema",
                column: "Libelle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schema_Metadonnee_IdMetadonnee",
                table: "Schema_Metadonnee",
                column: "IdMetadonnee");

            migrationBuilder.CreateIndex(
                name: "IX_Schema_Metadonnee_IdSchema",
                table: "Schema_Metadonnee",
                column: "IdSchema");

            migrationBuilder.AddForeignKey(
                name: "FK_Metadonnee_Documentation_IdDocumentation",
                table: "Metadonnee",
                column: "IdDocumentation",
                principalTable: "Documentation",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
