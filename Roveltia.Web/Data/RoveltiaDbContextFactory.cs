using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Roveltia.Web.Data;

/// <summary>Used by <c>dotnet ef</c> when no running app host is available.</summary>
public sealed class RoveltiaDbContextFactory : IDesignTimeDbContextFactory<RoveltiaDbContext>
{
    public RoveltiaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RoveltiaDbContext>();
        var conn =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=roveltia;Username=postgres;Password=postgres";
        optionsBuilder.UseNpgsql(conn);
        return new RoveltiaDbContext(optionsBuilder.Options);
    }
}
