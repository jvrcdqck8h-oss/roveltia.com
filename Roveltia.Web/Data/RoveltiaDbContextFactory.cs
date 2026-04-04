using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Roveltia.Web.Data;

public sealed class RoveltiaDbContextFactory : IDesignTimeDbContextFactory<RoveltiaDbContext>
{
    public RoveltiaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<RoveltiaDbContext>();
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=(localdb)\\mssqllocaldb;Database=Roveltia;Trusted_Connection=True;TrustServerCertificate=True";

        optionsBuilder.UseSqlServer(connectionString);

        return new RoveltiaDbContext(optionsBuilder.Options);
    }
}
