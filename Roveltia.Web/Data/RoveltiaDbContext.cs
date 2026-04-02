using Microsoft.EntityFrameworkCore;
using Roveltia.Web.Models;

namespace Roveltia.Web.Data;

public class RoveltiaDbContext : DbContext
{
    public RoveltiaDbContext(DbContextOptions<RoveltiaDbContext> options)
        : base(options)
    {
    }

    public DbSet<WaitlistSignup> WaitlistSignups => Set<WaitlistSignup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var signup = modelBuilder.Entity<WaitlistSignup>();
        signup.HasKey(e => e.Id);
        signup.Property(e => e.Email).HasMaxLength(320).IsRequired();
        signup.HasIndex(e => e.Email).IsUnique();
        signup.Property(e => e.CreatedAtUtc).IsRequired();
    }
}
