using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using Roveltia.Web.Data;
using Roveltia.Web.Models;

namespace Roveltia.Web.Services;

public enum WaitlistSignupResult
{
    Subscribed,
    AlreadySubscribed,
    SuspectedBot,
    Unsubscribed,
    NotSubscribed,
    InvalidUnsubscribeLink,
    Failed,
}

public sealed record WaitlistSubscribeRequest(string Email, string? Website);

public interface IWaitlistSignupService
{
    Task<WaitlistSignupResult> SubscribeAsync(
        WaitlistSubscribeRequest request,
        CancellationToken cancellationToken = default);

    Task<WaitlistSignupResult> UnsubscribeAsync(string email, string token, CancellationToken cancellationToken = default);
}

public sealed class WaitlistSignupService(
    IDbContextFactory<RoveltiaDbContext> dbContextFactory,
    IWaitlistEmailSender waitlistEmailSender,
    ILogger<WaitlistSignupService> logger) : IWaitlistSignupService
{
    public async Task<WaitlistSignupResult> SubscribeAsync(
        WaitlistSubscribeRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        if (IsLikelyBot(request))
        {
            logger.LogInformation("Blocked suspected bot waitlist signup for {Email}.", normalizedEmail);
            return WaitlistSignupResult.SuspectedBot;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingSignup = await dbContext.WaitlistSignups
            .SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (existingSignup is not null)
        {
            return WaitlistSignupResult.AlreadySubscribed;
        }

        var signup = new WaitlistSignup
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            UnsubscribeToken = CreateUnsubscribeToken(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        dbContext.WaitlistSignups.Add(signup);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await waitlistEmailSender.SendWelcomeEmailAsync(signup, cancellationToken);
            return WaitlistSignupResult.Subscribed;
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Waitlist subscribe hit a duplicate insert for {Email}.", normalizedEmail);
            return WaitlistSignupResult.AlreadySubscribed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Waitlist subscribe failed for {Email}.", normalizedEmail);
            return WaitlistSignupResult.Failed;
        }
    }

    public async Task<WaitlistSignupResult> UnsubscribeAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedToken = NormalizeToken(token);

        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(normalizedToken))
        {
            return WaitlistSignupResult.InvalidUnsubscribeLink;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingSignup = await dbContext.WaitlistSignups
            .SingleOrDefaultAsync(
                x => x.Email == normalizedEmail && x.UnsubscribeToken == normalizedToken,
                cancellationToken);

        if (existingSignup is null)
        {
            return WaitlistSignupResult.InvalidUnsubscribeLink;
        }

        dbContext.WaitlistSignups.Remove(existingSignup);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return WaitlistSignupResult.Unsubscribed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Waitlist unsubscribe failed for {Email}.", normalizedEmail);
            return WaitlistSignupResult.Failed;
        }
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static string NormalizeToken(string token) => token.Trim();

    private static bool IsLikelyBot(WaitlistSubscribeRequest request) =>
        !string.IsNullOrWhiteSpace(request.Website);

    private static string CreateUnsubscribeToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
