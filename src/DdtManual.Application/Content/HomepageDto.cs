namespace DdtManual.Application.Content;

/// <summary>Homepage single-type payload from the CMS (Strapi <c>homepage</c>), aligned with the main Service Manual.</summary>
public sealed class HomepageDto
{
    public string Title { get; init; } = string.Empty;
    public string? Headline { get; init; }
    public string? Html { get; init; }
    public string? CustomJs { get; init; }
    public string? CustomCss { get; init; }

    /// <summary>Static layout sample when the CMS is unavailable in Development only.</summary>
    public static HomepageDto DevelopmentPlaceholder()
    {
        const string headline =
            """
            <div class="dfe-f-hero">
              <div class="dfe-f-hero__container">
                <div class="dfe-f-hero__content">
                  <h1 class="dfe-f-hero__title">
                    <span class="dfe-f-hero__title-main">DdT Manual</span>
                    <span class="dfe-f-hero__title-sub">Design and delivery guidance</span>
                  </h1>
                  <p class="dfe-f-hero__intro govuk-body-l">Connect this app to your Strapi <strong>homepage</strong> single type to replace this placeholder.</p>
                  <div class="dfe-f-hero__actions">
                    <a class="dfe-f-btn dfe-f-btn--primary" href="/guidance">Browse guidance</a>
                    <a class="dfe-f-btn dfe-f-btn--secondary" href="/search">Search</a>
                  </div>
                </div>
              </div>
            </div>
            """;

        const string body =
            """
            <div class="dfe-f-homepage-section">
              <p class="govuk-body">Set <code>Cms:BaseUrl</code> (and optional <code>Cms:ApiToken</code>) so the homepage is loaded from the CMS.</p>
            </div>
            """;

        return new HomepageDto
        {
            Title = "DdT Manual",
            Headline = headline,
            Html = body,
        };
    }
}
