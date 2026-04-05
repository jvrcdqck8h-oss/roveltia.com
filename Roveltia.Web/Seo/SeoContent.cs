namespace Roveltia.Web.Seo;

/// <summary>Central copy for meta tags, Open Graph, and JSON-LD (keep in sync).</summary>
public static class SeoContent
{
    public const string SiteUrl = "https://roveltia.com";
    public const string SiteName = "Roveltia";
    public const string SiteTagline = "Build your own offline turntable for smooth 360° product photography";

    /// <summary>Primary document title.</summary>
    public const string HomeTitle = "Roveltia | Build your own offline turntable for smooth 360° product photography";

    /// <summary>Meta description for search and social previews.</summary>
    public const string HomeDescription =
        "Roveltia is a digital build kit for a 3D-printable smart turntable—smooth 360° product photography without cloud software. Assemble it yourself, connect to built-in Wi-Fi, and control spins, steps, sequences, and flash captures from a browser or with a compatible IR remote.";

    public static string HomeCanonical => $"{SiteUrl}/";

    /// <summary>Open Graph preview image (1200x630).</summary>
    public static string OgImageUrl => $"{SiteUrl}/og/roveltia-home.jpg";
    public const string OgImageAlt = "Roveltia smart turntable landing page preview";

    public const string OgImageWidth = "1200";
    public const string OgImageHeight = "630";

    public const string Locale = "en_US";
}
