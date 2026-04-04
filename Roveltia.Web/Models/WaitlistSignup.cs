namespace Roveltia.Web.Models;

public sealed class WaitlistSignup
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string UnsubscribeToken { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
