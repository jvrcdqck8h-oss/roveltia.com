namespace Roveltia.Web.Seo;

/// <summary>Central copy for meta tags, Open Graph, and JSON-LD (keep in sync).</summary>
public static class SeoContent
{
    public const string SiteUrl = "https://roveltia.com";
    public const string SiteName = "Roveltia";
    public const string SiteTagline = "Build your own smart turntable with a 3D-printed chassis for smooth, repeatable 360 product photography";

    /// <summary>Primary document title.</summary>
    public const string HomeTitle = "Roveltia | Build Your Own Smart Turntable for 360 Product Photography";

    /// <summary>Meta description for search and social previews.</summary>
    public const string HomeDescription =
        "Roveltia is a digital build kit for building your own smart turntable for smooth, repeatable 360 product photography. Print the chassis, assemble the hardware, and control the table from the Roveltia mobile app over Bluetooth: multi-frame sequences, 360° video, animated GIFs, and a local gallery on your phone.";

    public static string HomeCanonical => $"{SiteUrl}/";

    /// <summary>Open Graph preview image (1200x630).</summary>
    public static string OgImageUrl => $"{SiteUrl}/og/roveltia-home.jpg";
    public const string OgImageAlt = "Roveltia smart turntable landing page preview";

    public const string OgImageWidth = "1200";
    public const string OgImageHeight = "630";

    public const string Locale = "en_US";
}
