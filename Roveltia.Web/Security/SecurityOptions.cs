namespace Roveltia.Web.Security;

public sealed class SecurityOptions
{
    public WaitlistRateLimitOptions Waitlist { get; set; } = new();

    public AdminRateLimitOptions Admin { get; set; } = new();
}

public sealed class WaitlistRateLimitOptions
{
    public int MaxAttemptsPerIpWindow { get; set; } = 10;

    public int IpWindowMinutes { get; set; } = 10;

    public int MaxAttemptsPerEmailWindow { get; set; } = 3;

    public int EmailWindowMinutes { get; set; } = 60;
}

public sealed class AdminRateLimitOptions
{
    public int RequestsPerMinute { get; set; } = 5;
}
