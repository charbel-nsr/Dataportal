using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dataportal.Migrations
{
    /// <inheritdoc />
    public partial class SecondMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MotDePass",
                table: "utilisateur",
                newName: "MotDePasseHash");

            migrationBuilder.AlterColumn<string>(
                name: "Prenom",
                table: "utilisateur",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "utilisateur",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "utilisateur",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateApprobation",
                table: "utilisateur",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DateModification",
                table: "utilisateur",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DernierLogin",
                table: "utilisateur",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IdEntreprise",
                table: "utilisateur",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IdRole",
                table: "utilisateur",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Appareil",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Capacite = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appareil", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Donnees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NomDeLaTable = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NombreDeCapteurs = table.Column<int>(type: "int", nullable: false),
                    FrequenceDeCollect = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DateAjouter = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    StartTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donnees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonneesContexteEnvironnemental",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NomDeLaTable = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateAjouter = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    StartTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonneesContexteEnvironnemental", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonneesEventLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NomDeLaTable = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DateAjouter = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    StartTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    NombreDEvents = table.Column<int>(type: "int", nullable: false),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonneesEventLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Entreprise",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Actif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entreprise", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Licence",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Licence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Schema",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schema", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Site",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Emplacement = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Site", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatutDeLaDemande",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutDeLaDemande", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Visibilite",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visibilite", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DomaineEmail",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Domaine = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdEntreprise = table.Column<int>(type: "int", nullable: false),
                    DomaineActif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DomaineEmail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DomaineEmail_Entreprise_IdEntreprise",
                        column: x => x.IdEntreprise,
                        principalTable: "Entreprise",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DemandeDeCompte",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prenom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MotDePasseHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdEntreprise = table.Column<int>(type: "int", nullable: false),
                    IdStatutDeLaDemande = table.Column<int>(type: "int", nullable: false),
                    EmailVerifie = table.Column<bool>(type: "bit", nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandeDeCompte", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandeDeCompte_Entreprise_IdEntreprise",
                        column: x => x.IdEntreprise,
                        principalTable: "Entreprise",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DemandeDeCompte_StatutDeLaDemande_IdStatutDeLaDemande",
                        column: x => x.IdStatutDeLaDemande,
                        principalTable: "StatutDeLaDemande",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Documentation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Lien = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documentation", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Metadonnee",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IdLicence = table.Column<int>(type: "int", nullable: false),
                    TailleDesDonnees = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NombreDeTelechargements = table.Column<int>(type: "int", nullable: false),
                    DernierMiseAJour = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IdDocumentation = table.Column<int>(type: "int", nullable: false),
                    SeriesTemporelles = table.Column<bool>(type: "bit", nullable: false),
                    QualiteDesDonnees = table.Column<int>(type: "int", nullable: false),
                    IdSite = table.Column<int>(type: "int", nullable: false),
                    IdVisibilite = table.Column<int>(type: "int", nullable: false),
                    IdUtilisateur = table.Column<int>(type: "int", nullable: false),
                    StartTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AutoriserApi = table.Column<bool>(type: "bit", nullable: false),
                    Anonymiser = table.Column<bool>(type: "bit", nullable: false),
                    AutoriserLeTelechargement = table.Column<bool>(type: "bit", nullable: false),
                    VisualiserLesdonnees = table.Column<bool>(type: "bit", nullable: false),
                    AutoriserLesSQL = table.Column<bool>(type: "bit", nullable: false),
                    IdDonnees = table.Column<int>(type: "int", nullable: false),
                    IdDonneesEventLogs = table.Column<int>(type: "int", nullable: false),
                    IdDonneesContexteEnvironnemental = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metadonnee", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Metadonnee_Documentation_IdDocumentation",
                        column: x => x.IdDocumentation,
                        principalTable: "Documentation",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Metadonnee_DonneesContexteEnvironnemental_IdDonneesContexteEnvironnemental",
                        column: x => x.IdDonneesContexteEnvironnemental,
                        principalTable: "DonneesContexteEnvironnemental",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Metadonnee_DonneesEventLogs_IdDonneesEventLogs",
                        column: x => x.IdDonneesEventLogs,
                        principalTable: "DonneesEventLogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Metadonnee_Donnees_IdDonnees",
                        column: x => x.IdDonnees,
                        principalTable: "Donnees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Metadonnee_Licence_IdLicence",
                        column: x => x.IdLicence,
                        principalTable: "Licence",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Metadonnee_Site_IdSite",
                        column: x => x.IdSite,
                        principalTable: "Site",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Metadonnee_Visibilite_IdVisibilite",
                        column: x => x.IdVisibilite,
                        principalTable: "Visibilite",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Metadonnee_utilisateur_IdUtilisateur",
                        column: x => x.IdUtilisateur,
                        principalTable: "utilisateur",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Historique",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Lien = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Historique", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Historique_Metadonnee_IdMetadonnee",
                        column: x => x.IdMetadonnee,
                        principalTable: "Metadonnee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Metadonnee_Appareil",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false),
                    IdAppareil = table.Column<int>(type: "int", nullable: false),
                    IdAppareilDansDonnees = table.Column<int>(type: "int", nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Metadonnee_Appareil", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Metadonnee_Appareil_Appareil_IdAppareil",
                        column: x => x.IdAppareil,
                        principalTable: "Appareil",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Metadonnee_Appareil_Metadonnee_IdMetadonnee",
                        column: x => x.IdMetadonnee,
                        principalTable: "Metadonnee",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Schema_Metadonnee",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdSchema = table.Column<int>(type: "int", nullable: false),
                    IdMetadonnee = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
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
                name: "IX_utilisateur_Email",
                table: "utilisateur",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_utilisateur_IdEntreprise",
                table: "utilisateur",
                column: "IdEntreprise");

            migrationBuilder.CreateIndex(
                name: "IX_utilisateur_IdRole",
                table: "utilisateur",
                column: "IdRole");

            migrationBuilder.CreateIndex(
                name: "IX_DemandeDeCompte_Email",
                table: "DemandeDeCompte",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DemandeDeCompte_IdEntreprise",
                table: "DemandeDeCompte",
                column: "IdEntreprise");

            migrationBuilder.CreateIndex(
                name: "IX_DemandeDeCompte_IdStatutDeLaDemande",
                table: "DemandeDeCompte",
                column: "IdStatutDeLaDemande");

            migrationBuilder.CreateIndex(
                name: "IX_Documentation_IdMetadonnee",
                table: "Documentation",
                column: "IdMetadonnee");

            migrationBuilder.CreateIndex(
                name: "IX_DomaineEmail_IdEntreprise",
                table: "DomaineEmail",
                column: "IdEntreprise");

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

            migrationBuilder.CreateIndex(
                name: "IX_Donnees_NomDeLaTable",
                table: "Donnees",
                column: "NomDeLaTable",
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
                name: "IX_DonneesContexteEnvironnemental_NomDeLaTable",
                table: "DonneesContexteEnvironnemental",
                column: "NomDeLaTable",
                unique: true);

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
                name: "IX_DonneesEventLogs_NomDeLaTable",
                table: "DonneesEventLogs",
                column: "NomDeLaTable",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Historique_IdMetadonnee",
                table: "Historique",
                column: "IdMetadonnee");

            migrationBuilder.CreateIndex(
                name: "IX_Licence_Libelle",
                table: "Licence",
                column: "Libelle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdDocumentation",
                table: "Metadonnee",
                column: "IdDocumentation");

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdDonnees",
                table: "Metadonnee",
                column: "IdDonnees",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdLicence",
                table: "Metadonnee",
                column: "IdLicence");

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdSite",
                table: "Metadonnee",
                column: "IdSite");

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdUtilisateur",
                table: "Metadonnee",
                column: "IdUtilisateur");

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_IdVisibilite",
                table: "Metadonnee",
                column: "IdVisibilite");

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_Appareil_IdAppareil",
                table: "Metadonnee_Appareil",
                column: "IdAppareil");

            migrationBuilder.CreateIndex(
                name: "IX_Metadonnee_Appareil_IdMetadonnee",
                table: "Metadonnee_Appareil",
                column: "IdMetadonnee");

            migrationBuilder.CreateIndex(
                name: "IX_Role_Libelle",
                table: "Role",
                column: "Libelle",
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "IX_StatutDeLaDemande_Libelle",
                table: "StatutDeLaDemande",
                column: "Libelle",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Visibilite_Libelle",
                table: "Visibilite",
                column: "Libelle",
                unique: true);

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

            migrationBuilder.AddForeignKey(
                name: "FK_Documentation_Metadonnee_IdMetadonnee",
                table: "Documentation",
                column: "IdMetadonnee",
                principalTable: "Metadonnee",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_utilisateur_Entreprise_IdEntreprise",
                table: "utilisateur");

            migrationBuilder.DropForeignKey(
                name: "FK_utilisateur_Role_IdRole",
                table: "utilisateur");

            migrationBuilder.DropForeignKey(
                name: "FK_Documentation_Metadonnee_IdMetadonnee",
                table: "Documentation");

            migrationBuilder.DropTable(
                name: "DemandeDeCompte");

            migrationBuilder.DropTable(
                name: "DomaineEmail");

            migrationBuilder.DropTable(
                name: "Historique");

            migrationBuilder.DropTable(
                name: "Metadonnee_Appareil");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "Schema_Metadonnee");

            migrationBuilder.DropTable(
                name: "StatutDeLaDemande");

            migrationBuilder.DropTable(
                name: "Entreprise");

            migrationBuilder.DropTable(
                name: "Appareil");

            migrationBuilder.DropTable(
                name: "Schema");

            migrationBuilder.DropTable(
                name: "Metadonnee");

            migrationBuilder.DropTable(
                name: "Documentation");

            migrationBuilder.DropTable(
                name: "DonneesContexteEnvironnemental");

            migrationBuilder.DropTable(
                name: "DonneesEventLogs");

            migrationBuilder.DropTable(
                name: "Donnees");

            migrationBuilder.DropTable(
                name: "Licence");

            migrationBuilder.DropTable(
                name: "Site");

            migrationBuilder.DropTable(
                name: "Visibilite");

            migrationBuilder.DropIndex(
                name: "IX_utilisateur_Email",
                table: "utilisateur");

            migrationBuilder.DropIndex(
                name: "IX_utilisateur_IdEntreprise",
                table: "utilisateur");

            migrationBuilder.DropIndex(
                name: "IX_utilisateur_IdRole",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "DateApprobation",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "DateModification",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "DernierLogin",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "IdEntreprise",
                table: "utilisateur");

            migrationBuilder.DropColumn(
                name: "IdRole",
                table: "utilisateur");

            migrationBuilder.RenameColumn(
                name: "MotDePasseHash",
                table: "utilisateur",
                newName: "MotDePass");

            migrationBuilder.AlterColumn<string>(
                name: "Prenom",
                table: "utilisateur",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Nom",
                table: "utilisateur",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "utilisateur",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
