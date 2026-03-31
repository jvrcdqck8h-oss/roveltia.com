namespace Roveltia.Web.Seo;

/// <summary>Central copy for meta tags, Open Graph, and JSON-LD (keep in sync).</summary>
public static class SeoContent
{
    public const string SiteUrl = "https://roveltia.com";
    public const string SiteName = "Roveltia";

    /// <summary>Primary document title.</summary>
    public const string HomeTitle = "Roveltia | 3D-Printable Smart Turntable for Product Photos & 360s";

    /// <summary>Meta description for search and social previews.</summary>
    public const string HomeDescription =
        "Digital download only: a 3D-printable smart turntable for product photos, 360s, and video with phone control, shutter trigger, and advanced photo workflows.";

    public static string HomeCanonical => $"{SiteUrl}/";

    /// <summary>Open Graph preview image (1200x630).</summary>
    public static string OgImageUrl => $"{SiteUrl}/og/roveltia-home.jpg";

    public const string OgImageWidth = "1200";
    public const string OgImageHeight = "630";

    public const string Locale = "en_US";
}
