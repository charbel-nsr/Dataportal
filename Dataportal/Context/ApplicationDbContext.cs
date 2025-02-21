using Dataportal.Models;
using Microsoft.EntityFrameworkCore;

namespace Dataportal.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> contextOptions): base(contextOptions)
        {

        }

        //Code - Approach
        public DbSet<Utilisateur> utilisateur { get; set; }
    }
}