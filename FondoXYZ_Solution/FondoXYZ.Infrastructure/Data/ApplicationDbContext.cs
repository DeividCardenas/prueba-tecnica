using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FondoXYZ.Domain.Entities;

namespace FondoXYZ.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<Usuario, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Mapeo de las tablas principales de la base de datos
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Sede> Sedes { get; set; }
        public DbSet<Alojamiento> Alojamientos { get; set; }
        public DbSet<Tarifa> Tarifas { get; set; }
        public DbSet<Reserva> Reservas { get; set; }
        public DbSet<Temporada> Temporadas { get; set; }
        public DbSet<Festivo> Festivos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Mapeo de tablas de Identity para mantener nombres limpios
            modelBuilder.Entity<Usuario>().ToTable("Usuarios");
            modelBuilder.Entity<IdentityRole<int>>().ToTable("Roles");
            modelBuilder.Entity<IdentityUserRole<int>>().ToTable("UsuarioRoles");
            modelBuilder.Entity<IdentityUserClaim<int>>().ToTable("UsuarioClaims");
            modelBuilder.Entity<IdentityUserLogin<int>>().ToTable("UsuarioLogins");
            modelBuilder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");
            modelBuilder.Entity<IdentityUserToken<int>>().ToTable("UsuarioTokens");

            modelBuilder.Entity<Reserva>()
                .HasOne(r => r.Usuario)
                .WithMany(u => u.Reservas)
                .HasForeignKey(r => r.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reserva>()
                .HasOne(r => r.Sede)
                .WithMany(s => s.Reservas)
                .HasForeignKey(r => r.SedeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reserva>()
                .HasOne(r => r.Alojamiento)
                .WithMany(a => a.Reservas)
                .HasForeignKey(r => r.AlojamientoId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}