using Microsoft.EntityFrameworkCore;
using Roveltia.Web.Models;

namespace Roveltia.Web.Data;

public sealed class RoveltiaDbContext(DbContextOptions<RoveltiaDbContext> options) : DbContext(options)
{
    public DbSet<WaitlistSignup> WaitlistSignups => Set<WaitlistSignup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WaitlistSignup>(entity =>
        {
            entity.ToTable("WaitlistSignups");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(320);

            entity.Property(x => x.UnsubscribeToken)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(x => x.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(x => x.Email)
                .IsUnique();

            entity.HasIndex(x => x.UnsubscribeToken)
                .IsUnique();
        });
    }
}
