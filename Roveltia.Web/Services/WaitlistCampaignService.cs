using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;
using Roveltia.Web.Data;
using Roveltia.Web.Models;

namespace Roveltia.Web.Services;

public sealed class WaitlistEmailOptions
{
    public string PublicBaseUrl { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string? FromName { get; set; }

    public string? ReplyToAddress { get; set; }

    public string WelcomeSubject { get; set; } = "Welcome to the Roveltia waitlist";

    public string WelcomeBody { get; set; } =
        "Hi there,\n\nThanks for joining the Roveltia waitlist. You'll hear from us when there is meaningful launch news, prototype progress, or campaign timing to share.\n\nUnsubscribe anytime: {{unsubscribe_url}}";

    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;
}

public sealed class CampaignAdminOptions
{
    public string CampaignApiKey { get; set; } = string.Empty;
}

public sealed record SendWaitlistCampaignRequest(
    string Subject,
    string Body,
    string? OnlyEmail = null,
    bool DryRun = false);

public sealed record SendWaitlistCampaignResult(
    bool Success,
    int MatchedRecipients,
    int SentCount,
    int FailedCount,
    bool DryRun,
    string Message,
    IReadOnlyList<string> FailedRecipients);

public interface IWaitlistEmailSender
{
    Task<bool> SendWelcomeEmailAsync(WaitlistSignup recipient, CancellationToken cancellationToken = default);

    Task<SendWaitlistCampaignResult> SendCampaignAsync(
        SendWaitlistCampaignRequest request,
        CancellationToken cancellationToken = default);
}

public interface IWaitlistCampaignService
{
    Task<SendWaitlistCampaignResult> SendCampaignAsync(
        SendWaitlistCampaignRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class WaitlistCampaignService(
    IDbContextFactory<RoveltiaDbContext> dbContextFactory,
    IOptions<WaitlistEmailOptions> emailOptions,
    ILogger<WaitlistCampaignService> logger) : IWaitlistCampaignService, IWaitlistEmailSender
{
    public async Task<bool> SendWelcomeEmailAsync(WaitlistSignup recipient, CancellationToken cancellationToken = default)
    {
        var options = emailOptions.Value;
        var emailConfigError = ValidateEmailOptions(options, dryRun: false);
        if (emailConfigError is not null)
        {
            logger.LogWarning("Welcome email skipped for {Email}: {Reason}", recipient.Email, emailConfigError);
            return false;
        }

        var request = new SendWaitlistCampaignRequest(
            options.WelcomeSubject,
            options.WelcomeBody,
            OnlyEmail: recipient.Email,
            DryRun: false);

        try
        {
            using var smtpClient = BuildSmtpClient(options);
            using var message = BuildMessage(options, request, recipient);
            await smtpClient.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            LogEmailSendFailure(ex, recipient.Email, options, "welcome email");
            return false;
        }
    }

    public async Task<SendWaitlistCampaignResult> SendCampaignAsync(
        SendWaitlistCampaignRequest request,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null)
        {
            return new SendWaitlistCampaignResult(
                Success: false,
                MatchedRecipients: 0,
                SentCount: 0,
                FailedCount: 0,
                DryRun: request.DryRun,
                Message: validationError,
                FailedRecipients: []);
        }

        var options = emailOptions.Value;
        var emailConfigError = ValidateEmailOptions(options, request.DryRun);
        if (emailConfigError is not null)
        {
            return new SendWaitlistCampaignResult(
                Success: false,
                MatchedRecipients: 0,
                SentCount: 0,
                FailedCount: 0,
                DryRun: request.DryRun,
                Message: emailConfigError,
                FailedRecipients: []);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.WaitlistSignups
            .AsNoTracking()
            .OrderBy(x => x.CreatedAtUtc)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.OnlyEmail))
        {
            var onlyEmail = request.OnlyEmail.Trim().ToLowerInvariant();
            query = query.Where(x => x.Email == onlyEmail);
        }

        var recipients = await query.ToListAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            return new SendWaitlistCampaignResult(
                Success: false,
                MatchedRecipients: 0,
                SentCount: 0,
                FailedCount: 0,
                DryRun: request.DryRun,
                Message: "No matching subscribed recipients were found.",
                FailedRecipients: []);
        }

        if (request.DryRun)
        {
            return new SendWaitlistCampaignResult(
                Success: true,
                MatchedRecipients: recipients.Count,
                SentCount: 0,
                FailedCount: 0,
                DryRun: true,
                Message: "Dry run completed. No emails were sent.",
                FailedRecipients: []);
        }

        using var smtpClient = BuildSmtpClient(options);
        var failedRecipients = new List<string>();
        var sentCount = 0;

        foreach (var recipient in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var message = BuildMessage(options, request, recipient);
                await smtpClient.SendMailAsync(message, cancellationToken);
                sentCount++;
            }
            catch (Exception ex)
            {
                LogEmailSendFailure(ex, recipient.Email, options, "waitlist campaign email");
                failedRecipients.Add(recipient.Email);
            }
        }

        var failedCount = failedRecipients.Count;
        var success = sentCount > 0 && failedCount == 0;
        var messageText = failedCount == 0
            ? $"Sent {sentCount} waitlist email(s)."
            : $"Sent {sentCount} waitlist email(s); {failedCount} failed.";

        return new SendWaitlistCampaignResult(
            Success: success,
            MatchedRecipients: recipients.Count,
            SentCount: sentCount,
            FailedCount: failedCount,
            DryRun: false,
            Message: messageText,
            FailedRecipients: failedRecipients);
    }

    private static string? ValidateRequest(SendWaitlistCampaignRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            return "Subject is required.";
        }

        if (request.Subject.Trim().Length > 200)
        {
            return "Subject must be 200 characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return "Body is required.";
        }

        if (request.Body.Trim().Length > 10000)
        {
            return "Body must be 10000 characters or fewer.";
        }

        if (!string.IsNullOrWhiteSpace(request.OnlyEmail) &&
            request.OnlyEmail.Trim().Length > 320)
        {
            return "OnlyEmail must be 320 characters or fewer.";
        }

        return null;
    }

    private static string? ValidateEmailOptions(WaitlistEmailOptions options, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(options.PublicBaseUrl))
        {
            return "Email:PublicBaseUrl is required.";
        }

        if (dryRun)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress) ||
            string.IsNullOrWhiteSpace(options.Smtp.Host) ||
            options.Smtp.Port <= 0 ||
            string.IsNullOrWhiteSpace(options.Smtp.Username) ||
            string.IsNullOrWhiteSpace(options.Smtp.Password))
        {
            return "Email SMTP settings are incomplete.";
        }

        return null;
    }

    private static SmtpClient BuildSmtpClient(WaitlistEmailOptions options)
    {
        return new SmtpClient(options.Smtp.Host, options.Smtp.Port)
        {
            EnableSsl = options.Smtp.EnableSsl,
            Credentials = new NetworkCredential(options.Smtp.Username, options.Smtp.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };
    }

    private static MailMessage BuildMessage(
        WaitlistEmailOptions options,
        SendWaitlistCampaignRequest request,
        WaitlistSignup recipient)
    {
        var unsubscribeUrl = BuildUnsubscribeUrl(options.PublicBaseUrl, recipient);
        var body = RenderBody(request.Body, recipient.Email, unsubscribeUrl);
        var from = string.IsNullOrWhiteSpace(options.FromName)
            ? new MailAddress(options.FromAddress)
            : new MailAddress(options.FromAddress, options.FromName);

        var message = new MailMessage
        {
            From = from,
            Subject = request.Subject.Trim(),
            Body = body,
            IsBodyHtml = false,
        };

        message.To.Add(recipient.Email);

        if (!string.IsNullOrWhiteSpace(options.ReplyToAddress))
        {
            message.ReplyToList.Add(new MailAddress(options.ReplyToAddress));
        }

        return message;
    }

    private static string BuildUnsubscribeUrl(string publicBaseUrl, WaitlistSignup recipient)
    {
        var normalizedBaseUrl = publicBaseUrl.Trim().TrimEnd('/');
        return QueryHelpers.AddQueryString(
            $"{normalizedBaseUrl}/unsubscribe",
            new Dictionary<string, string?>
            {
                ["email"] = recipient.Email,
                ["token"] = recipient.UnsubscribeToken,
            });
    }

    private static string RenderBody(string template, string recipientEmail, string unsubscribeUrl)
    {
        var renderedBody = template.Trim()
            .Replace("{{email}}", recipientEmail, StringComparison.OrdinalIgnoreCase)
            .Replace("{{unsubscribe_url}}", unsubscribeUrl, StringComparison.OrdinalIgnoreCase);

        if (renderedBody.Contains(unsubscribeUrl, StringComparison.Ordinal))
        {
            return renderedBody;
        }

        return $"{renderedBody}\n\nYou’re receiving this email because you joined the Roveltia waitlist.\nUnsubscribe: {unsubscribeUrl}";
    }

    private void LogEmailSendFailure(Exception exception, string recipientEmail, WaitlistEmailOptions options, string emailKind)
    {
        if (exception is SmtpException smtpException)
        {
            logger.LogError(
                smtpException,
                "Failed to send {EmailKind} to {RecipientEmail}. SMTP host={SmtpHost}, port={SmtpPort}, ssl={EnableSsl}, username={SmtpUsername}, from={FromAddress}. StatusCode={StatusCode}. This usually means the SMTP provider rejected the server or credentials rather than an app bug.",
                emailKind,
                recipientEmail,
                options.Smtp.Host,
                options.Smtp.Port,
                options.Smtp.EnableSsl,
                options.Smtp.Username,
                options.FromAddress,
                smtpException.StatusCode);

            return;
        }

        logger.LogError(
            exception,
            "Failed to send {EmailKind} to {RecipientEmail}. SMTP host={SmtpHost}, port={SmtpPort}, ssl={EnableSsl}, username={SmtpUsername}, from={FromAddress}.",
            emailKind,
            recipientEmail,
            options.Smtp.Host,
            options.Smtp.Port,
            options.Smtp.EnableSsl,
            options.Smtp.Username,
            options.FromAddress);
    }
}
