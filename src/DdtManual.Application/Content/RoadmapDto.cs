namespace DdtManual.Application.Content;

/// <summary>Roadmap single-type payload from the CMS (Strapi <c>roadmap</c>).</summary>
public sealed class RoadmapDto
{
    public string Title { get; init; } = string.Empty;

    public string? MetaDescription { get; init; }

    /// <summary>Main body from Strapi: markdown, blocks converted to markdown, or an HTML string from the CMS.</summary>
    public string? BodyMarkdown { get; init; }

    /// <summary>Optional update history (markdown or HTML, same rules as <see cref="BodyMarkdown"/>).</summary>
    public string? UpdateHistoryMarkdown { get; init; }

    /// <summary>Placeholder when the CMS is unavailable in Development only.</summary>
    public static RoadmapDto DevelopmentPlaceholder()
    {
        return new RoadmapDto
        {
            Title = "Roadmap",
            MetaDescription = "Connect Strapi single type roadmap to replace this placeholder.",
            BodyMarkdown =
                """
                Set `Cms:BaseUrl` (and optional `Cms:ApiToken`) so this page loads from the CMS **roadmap** single type.
                """,
        };
    }
}
