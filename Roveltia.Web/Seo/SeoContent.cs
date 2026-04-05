namespace Roveltia.Web.Seo;

/// <summary>Central copy for meta tags, Open Graph, and JSON-LD (keep in sync).</summary>
public static class SeoContent
{
    public const string SiteUrl = "https://roveltia.com";
    public const string SiteName = "Roveltia";
    public const string SiteTagline = "Build your own smart turntable for smooth, repeatable 360° product photography";

    /// <summary>Primary document title.</summary>
    public const string HomeTitle = "Roveltia | Build your own smart turntable for smooth, repeatable 360° product photography";

    /// <summary>Meta description for search and social previews.</summary>
    public const string HomeDescription =
        "Roveltia is a digital build kit for product photographers who want repeatable tabletop spins, cleaner capture cycles, and more control during 360 product shoots. Assemble the 3D-printable hardware and control steps, sequences, and flash-triggered captures from a browser or compatible IR remote.";

    public static string HomeCanonical => $"{SiteUrl}/";

    /// <summary>Open Graph preview image (1200x630).</summary>
    public static string OgImageUrl => $"{SiteUrl}/og/roveltia-home.jpg";
    public const string OgImageAlt = "Roveltia smart turntable landing page preview";

    public const string OgImageWidth = "1200";
    public const string OgImageHeight = "630";

    public const string Locale = "en_US";
}
