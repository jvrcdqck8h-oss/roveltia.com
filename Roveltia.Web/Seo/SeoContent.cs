namespace Roveltia.Web.Seo;

/// <summary>Central copy for meta tags, Open Graph, and JSON-LD (keep in sync).</summary>
public static class SeoContent
{
    public const string SiteUrl = "https://roveltia.com";
    public const string SiteName = "Roveltia";

    /// <summary>Primary document title.</summary>
    public const string HomeTitle = "Roveltia | Offline, 3D-printable smart turntable for smooth 360° product photos";

    /// <summary>Meta description for search and social previews.</summary>
    public const string HomeDescription =
        "An offline, 3D-printable smart turntable for smooth 360° product photography. Build it yourself. Control it from your phone. Shoot repeatable 360° product photos without cloud software.";

    public static string HomeCanonical => $"{SiteUrl}/";

    /// <summary>Open Graph preview image (1200x630).</summary>
    public static string OgImageUrl => $"{SiteUrl}/og/roveltia-home.jpg";

    public const string OgImageWidth = "1200";
    public const string OgImageHeight = "630";

    public const string Locale = "en_US";
}
