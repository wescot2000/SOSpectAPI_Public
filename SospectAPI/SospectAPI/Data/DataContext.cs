
using Microsoft.EntityFrameworkCore;
using SospectAPI.Data.Entities;

namespace SospectAPI.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {

        }

        public DbSet<Personas> Personas { get; set; }

        public DbSet<Ubicaciones> Ubicaciones { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Personas>().ToTable("personas").HasKey("persona_id");
            modelBuilder.Entity<Ubicaciones>().ToTable("ubicaciones").HasKey("ubicacion_id");
        }

    }
}
