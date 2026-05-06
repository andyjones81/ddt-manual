namespace DdtManual.Infrastructure.Standards;

public sealed class StandardsCmsOptions
{
    public const string SectionName = "StandardsCMS";

    /// <summary>HTTPS base URL of the Standards Strapi API (no trailing slash).</summary>
    public string? BaseUrl { get; set; }

    public string? ApiToken { get; set; }

    /// <summary>
    /// Optional map from public URL slug (key) to slug stored in Standards CMS (value).
    /// Use when bookmarks or external links use a different segment than <c>slug</c> in Strapi.
    /// </summary>
    public Dictionary<string, string>? SlugAliases { get; set; }
}
