using Microsoft.EntityFrameworkCore;
using WebApplicationPods.Models;

namespace WebApplicationPods.Data
{
    public class TenantDbContext : DbContext
    {
        public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

        public DbSet<LojaModel> Lojas => Set<LojaModel>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<LojaModel>(b =>
            {
                b.ToTable("Lojas");
                b.HasIndex(x => x.Subdominio).IsUnique();
                b.Property(x => x.Subdominio).HasMaxLength(60).IsRequired();
                b.Property(x => x.Ativa).HasDefaultValue(true);
            });
        }

    }
}
