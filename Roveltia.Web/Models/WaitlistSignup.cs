namespace Roveltia.Web.Models;

public class WaitlistSignup
{
    public Guid Id { get; set; }

    /// <summary>Normalized (trimmed, lower-case) email for uniqueness.</summary>
    public string Email { get; set; } = "";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
