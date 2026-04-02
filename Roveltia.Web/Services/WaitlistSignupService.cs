using Microsoft.EntityFrameworkCore;
using Npgsql;
using Roveltia.Web.Data;
using Roveltia.Web.Models;

namespace Roveltia.Web.Services;

public enum WaitlistSignupResult
{
    Added,
    AlreadyRegistered,
    Failed,
}

public interface IWaitlistSignupService
{
    Task<WaitlistSignupResult> TryAddAsync(string email, CancellationToken cancellationToken = default);
}

public sealed class WaitlistSignupService : IWaitlistSignupService
{
    private readonly RoveltiaDbContext _db;
    private readonly ILogger<WaitlistSignupService> _logger;

    public WaitlistSignupService(RoveltiaDbContext db, ILogger<WaitlistSignupService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<WaitlistSignupResult> TryAddAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        _db.WaitlistSignups.Add(new WaitlistSignup
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            return WaitlistSignupResult.Added;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return WaitlistSignupResult.AlreadyRegistered;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Waitlist signup failed.");
            return WaitlistSignupResult.Failed;
        }
    }
}
