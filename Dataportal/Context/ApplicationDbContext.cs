using Dataportal.Models;
using Microsoft.EntityFrameworkCore;

/*after changing this file, you should run the following commands:
 *Add-Migration YourMigrationName
 *Update-Database
 *
 *!!!Don't forget to change YourMigrationName by your migration name!!!
 */
namespace Dataportal.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> contextOptions): base(contextOptions)
        {

        }

        public DbSet<Appareil> Appareil { get; set; }
        public DbSet<DemandeDeCompte> DemandeDeCompte { get; set; }
        public DbSet<Documentation> Documentation { get; set; }
        public DbSet<DomaineEmail> DomaineEmail { get; set; }
        public DbSet<Donnees> Donnees { get; set; }
        public DbSet<DonneesContexteEnvironnemental> DonneesContexteEnvironnemental { get; set; }
        public DbSet<DonneesEventLogs> DonneesEventLogs { get; set; }
        public DbSet<Entreprise> Entreprise { get; set; }
        public DbSet<Historique> Historique { get; set; }
        public DbSet<Licence> Licence { get; set; }
        public DbSet<Metadonnee> Metadonnee { get; set; }
        public DbSet<Metadonnee_Appareil> Metadonnee_Appareil { get; set; }
        public DbSet<Role> Role { get; set; }
        public DbSet<Schema> Schema { get; set; }
        public DbSet<Schema_Metadonnee> Schema_Metadonnee { get; set; }
        public DbSet<Site> Site { get; set; }
        public DbSet<StatutDeLaDemande> StatutDeLaDemande { get; set; }
        public DbSet<Utilisateur> Utilisateur { get; set; }
        public DbSet<Visibilite> Visibilite { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Appareil>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Nom)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(a => a.Description)
                      .HasMaxLength(1000);
            });
            modelBuilder.Entity<DemandeDeCompte>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Nom)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(d => d.Prenom)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(d => d.Email)
                      .IsRequired();
                entity.Property(d => d.MotDePasseHash)
                      .IsRequired();
                entity.Property(d => d.Commentaire)
                      .HasMaxLength(1000);
                entity.Property(d => d.DateCreation)
                      .HasDefaultValueSql("GETUTCDATE()");
                entity.HasIndex(d => d.Email)
                      .IsUnique();
                entity.HasOne(d => d.Entreprise)
                      .WithMany(e => e.DemandeDeComptes)
                      .HasForeignKey(d => d.IdEntreprise);
                entity.HasOne(d => d.StatutDeLaDemande)
                      .WithMany(s => s.DemandeDeComptes)
                      .HasForeignKey(d => d.IdStatutDeLaDemande)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Documentation>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Libelle)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(d => d.Description)
                      .HasMaxLength(1000);
                entity.Property(d => d.Lien)
                      .IsRequired();
                entity.HasOne(d => d.Metadonnee)
                      .WithMany(m => m.Documentations)
                      .HasForeignKey(d => d.IdMetadonnee)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<DomaineEmail>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Domaine)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(d => d.DomaineActif)
                      .IsRequired();
                entity.HasOne(d => d.Entreprise)
                      .WithMany(e => e.DomaineEmails)
                      .HasForeignKey(d => d.IdEntreprise)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Donnees>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Libelle)
                      .IsRequired();
                entity.Property(d => d.Code)
                      .IsRequired();
                entity.Property(d => d.NomDeLaTable)
                      .IsRequired();
                entity.Property(d => d.Description);
                entity.Property(d => d.FrequenceDeCollect)
                      .HasMaxLength(50);
                entity.Property(d => d.DateAjouter)
                      .HasDefaultValueSql("GETUTCDATE()");
                entity.Property(d => d.StartTimestamp);
                entity.Property(d => d.EndTimestamp);
                entity.HasIndex(e => e.Libelle)
                      .IsUnique();
                entity.HasIndex(e => e.Code)
                      .IsUnique();
                entity.HasIndex(e => e.NomDeLaTable)
                      .IsUnique();
            });
            modelBuilder.Entity<DonneesContexteEnvironnemental>(entity =>
            {
                entity.HasKey(d => d.Id);
                entity.Property(d => d.Libelle)
                      .IsRequired();
                entity.Property(d => d.Code)
                      .IsRequired();
                entity.Property(d => d.NomDeLaTable)
                      .IsRequired();
                entity.Property(d => d.Description);
                entity.Property(d => d.DateAjouter)
                      .HasDefaultValueSql("GETUTCDATE()");
                entity.Property(d => d.StartTimestamp);
                entity.Property(d => d.EndTimestamp);
                entity.HasIndex(e => e.Libelle)
                      .IsUnique();
                entity.HasIndex(e => e.Code)
                      .IsUnique();
                entity.HasIndex(e => e.NomDeLaTable)
                      .IsUnique();
            });
            modelBuilder.Entity<DonneesEventLogs>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Libelle)
                      .IsRequired();
                entity.Property(e => e.Code)
                      .IsRequired();
                entity.Property(e => e.NomDeLaTable)
                      .IsRequired();
                entity.Property(e => e.Description)
                      .HasMaxLength(1000);
                entity.Property(e => e.DateAjouter)
                      .HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.StartTimestamp);
                entity.Property(e => e.EndTimestamp);
                entity.HasIndex(e => e.Libelle)
                      .IsUnique();
                entity.HasIndex(e => e.Code)
                      .IsUnique();
                entity.HasIndex(e => e.NomDeLaTable)
                      .IsUnique();
            });
            modelBuilder.Entity<Entreprise>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Nom)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.HasMany(e => e.DomaineEmails)
                      .WithOne(d => d.Entreprise)
                      .HasForeignKey(d => d.IdEntreprise)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(e => e.Utilisateurs)
                      .WithOne(u => u.Entreprise)
                      .HasForeignKey(u => u.IdEntreprise)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasMany(e => e.DemandeDeComptes)
                      .WithOne(d => d.Entreprise)
                      .HasForeignKey(d => d.IdEntreprise)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Historique>(entity =>
            {
                entity.HasKey(h => h.Id);
                entity.Property(h => h.Date)
                      .IsRequired();
                entity.Property(h => h.Lien)
                      .HasMaxLength(500);
                entity.Property(h => h.Description)
                      .HasMaxLength(1000);
                entity.HasOne(h => h.Metadonnee)
                      .WithMany(m => m.Historiques)
                      .HasForeignKey(h => h.IdMetadonnee)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Licence>(entity =>
            {
                entity.HasKey(l => l.Id);
                entity.Property(l => l.Libelle)
                      .IsRequired()
                      .HasMaxLength(50);
                entity.Property(l => l.Description)
                      .HasMaxLength(500);
                entity.HasIndex(l => l.Libelle)
                      .IsUnique();
                entity.HasMany(l => l.Metadonnees)
                      .WithOne(m => m.Licence)
                      .HasForeignKey(m => m.IdLicence)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Metadonnee>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Nom)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(m => m.Description)
                      .IsRequired();
                entity.Property(m => m.TailleDesDonnees)
                      .HasMaxLength(10);
                entity.HasOne(m => m.Licence)
                      .WithMany(l => l.Metadonnees)
                      .HasForeignKey(m => m.IdLicence);
                entity.HasMany(m => m.Documentations)
                      .WithOne(d => d.Metadonnee)
                      .HasForeignKey(d => d.IdMetadonnee);
                entity.HasOne(m => m.Site)
                      .WithMany(s => s.Metadonnees)
                      .HasForeignKey(m => m.IdSite);
                entity.HasOne(m => m.Visibilite)
                      .WithMany(v => v.Metadonnees)
                      .HasForeignKey(m => m.IdVisibilite);
                entity.HasOne(m => m.Utilisateur)
                      .WithMany(u => u.Metadonnees)
                      .HasForeignKey(m => m.IdUtilisateur);
                entity.Property(m => m.EndTimestamp);
                entity.Property(m => m.StartTimestamp);
                entity.HasMany(m => m.Historiques)
                      .WithOne(h => h.Metadonnee)
                      .HasForeignKey(h => h.IdMetadonnee);
                entity.HasOne(m => m.Donnees)
                      .WithOne(d => d.Metadonnee)
                      .HasForeignKey<Metadonnee>(m => m.IdDonnees);
                entity.HasOne(m => m.DonneesEventLogs)
                      .WithOne(d => d.Metadonnee)
                      .HasForeignKey<Metadonnee>(m => m.IdDonneesEventLogs);
                entity.HasOne(m => m.DonneesContexteEnvironnemental)
                      .WithOne(d => d.Metadonnee)
                      .HasForeignKey<Metadonnee>(m => m.IdDonneesContexteEnvironnemental);
            });
            modelBuilder.Entity<Metadonnee_Appareil>(entity =>
            {
                entity.HasKey(ma => ma.Id);
                entity.HasOne(ma => ma.Metadonnee)
                      .WithMany(m => m.Metadonnee_Appareils)
                      .HasForeignKey(ma => ma.IdMetadonnee)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(ma => ma.Appareil)
                      .WithMany(a => a.Metadonnee_Appareils)
                      .HasForeignKey(ma => ma.IdAppareil)
                      .OnDelete(DeleteBehavior.Restrict)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Libelle)
                      .IsRequired()
                      .HasMaxLength(50);
                entity.Property(r => r.Description)
                      .HasMaxLength(200);
                entity.HasIndex(r => r.Libelle)
                      .IsUnique();
                entity.HasMany(r => r.Utilisateurs)
                      .WithOne(u => u.Role)
                      .HasForeignKey(u => u.IdRole)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Schema>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Libelle)
                      .HasMaxLength(100);
                entity.HasIndex(s => s.Libelle)
                      .IsUnique();
                entity.Property(s => s.Description)
                      .HasMaxLength(1000);
            });
            modelBuilder.Entity<Schema_Metadonnee>(entity =>
            {
                entity.HasKey(sm => sm.Id);
                entity.Property(sm => sm.Description)
                      .HasMaxLength(1000);
                entity.HasOne(sm => sm.Schema)
                      .WithMany(s => s.schema_Metadonnees)
                      .HasForeignKey(sm => sm.IdSchema)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(sm => sm.Metadonnee)
                      .WithMany(m => m.schema_Metadonnees)
                      .HasForeignKey(sm => sm.IdMetadonnee)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Site>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Nom)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(s => s.Description)
                      .HasMaxLength(1000);
                entity.HasMany(s => s.Metadonnees)
                      .WithOne(m => m.Site)
                      .HasForeignKey(m => m.IdSite)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<StatutDeLaDemande>(entity =>
            {
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Libelle)
                      .IsRequired()
                      .HasMaxLength(50);
                entity.Property(s => s.Description)
                      .HasMaxLength(200);
                entity.HasIndex(s => s.Libelle)
                      .IsUnique();
                entity.HasMany(s => s.DemandeDeComptes)
                      .WithOne(d => d.StatutDeLaDemande)
                      .HasForeignKey(d => d.IdStatutDeLaDemande)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Utilisateur>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.Nom)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(u => u.Prenom)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(u => u.Email)
                      .IsRequired();
                entity.Property(u => u.MotDePasseHash)
                      .IsRequired();
                entity.HasIndex(u => u.Email)
                      .IsUnique();
                entity.Property(u => u.NbrEchecsAcces)
                      .HasDefaultValue(0);
                entity.Property(u => u.FinLockout);
                entity.HasOne(u => u.Entreprise)
                      .WithMany(e => e.Utilisateurs)
                      .HasForeignKey(u => u.IdEntreprise)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(u => u.Role)
                      .WithMany(r => r.Utilisateurs)
                      .HasForeignKey(u => u.IdRole)
                      .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<Visibilite>(entity =>
            {
                entity.HasKey(v => v.Id);
                entity.Property(v => v.Libelle)
                      .IsRequired()
                      .HasMaxLength(100);
                entity.Property(v => v.Description)
                      .HasMaxLength(1000);
                entity.HasIndex(v => v.Libelle)
                      .IsUnique();
                entity.HasMany(v => v.Metadonnees)
                      .WithOne(m => m.Visibilite)
                      .HasForeignKey(m => m.IdVisibilite)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}