namespace Roveltia.Web.Seo;

/// <summary>Central copy for meta tags, Open Graph, and JSON-LD (keep in sync).</summary>
public static class SeoContent
{
    public const string SiteUrl = "https://roveltia.com";
    public const string SiteName = "Roveltia";

    /// <summary>Primary document title.</summary>
    public const string HomeTitle = "Roveltia | Offline Smart Turntable for Product Photography (MakerWorld)";

    /// <summary>Meta description for search and social previews.</summary>
    public const string HomeDescription =
        "Fully 3D-printable, ESP32-C6 powered turntable for 360° product photos and video spins. Offline, no cloud, no subscriptions. Coming soon to MakerWorld Crowdfunding.";

    public static string HomeCanonical => $"{SiteUrl}/";

    /// <summary>Open Graph preview image (1200x630).</summary>
    public static string OgImageUrl => $"{SiteUrl}/og/roveltia-home.jpg";

    public const string OgImageWidth = "1200";
    public const string OgImageHeight = "630";

    public const string Locale = "en_US";
}
