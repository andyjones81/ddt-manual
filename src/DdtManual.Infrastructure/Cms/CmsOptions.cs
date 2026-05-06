namespace DdtManual.Infrastructure.Cms;

public sealed class CmsOptions
{
    public const string SectionName = "Cms";

    /// <summary>HTTPS base URL of the CMS API (no trailing slash).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Optional bearer token for authenticated CMS requests (prefer secret store in production).</summary>
    public string? ApiToken { get; set; }
}
