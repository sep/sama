using Microsoft.EntityFrameworkCore;
using sama.Models;
using System;

namespace sama
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Endpoint> Endpoints { get; set; } = default!;
        public DbSet<ApplicationUser> Users { get; set; } = default!;
        public DbSet<Setting> Settings { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder
                .Entity<ApplicationUser>()
                .Property(e => e.Id)
                .HasConversion(toDb => toDb.ToString("D").ToLower(), fromDb => Guid.Parse(fromDb));

            modelBuilder
                .Entity<Setting>()
                .Property(e => e.Id)
                .HasConversion(toDb => toDb.ToString("D").ToLower(), fromDb => Guid.Parse(fromDb));
        }
    }
}
