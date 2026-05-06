using DdtManual.Application.Content;

namespace DdtManual.Application.Abstractions;

/// <summary>
/// Reads published content from the CMS (Strapi or equivalent). Implement in Infrastructure.
/// </summary>
public interface ICmsContentClient
{
    /// <summary>Returns true when the CMS base URL is configured and reachable (implementation-defined).</summary>
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches the homepage single type (<c>GET api/homepage</c> with projected fields).</summary>
    Task<HomepageDto?> GetHomepageAsync(CancellationToken cancellationToken = default);

    /// <summary>Fetches the roadmap single type (<c>GET api/roadmap</c> with projected fields).</summary>
    Task<RoadmapDto?> GetRoadmapAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// All published navigable content from Strapi (articles, collections, guides, guide pages, roadmap),
    /// matching the Service Manual content index aggregation.
    /// </summary>
    Task<IReadOnlyList<ContentIndexItemDto>> GetPublishedContentIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>Single collection by slug (same Strapi query as Service Manual <c>GetCollectionBySlugAsync</c>).</summary>
    Task<CollectionDetailDto?> GetCollectionBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Detailed guide overview by slug (same Strapi query shape as Service Manual).</summary>
    Task<DetailedGuideOverviewDto?> GetDetailedGuideBySlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>Detailed guide child page by guide slug + page slug (two-step fetch like Service Manual).</summary>
    Task<DetailedGuideChildPageDto?> GetDetailedGuidePageBySlugAsync(string guideSlug, string pageSlug, CancellationToken cancellationToken = default);

    /// <summary>Guidance index: areas and collection/guide cards from Strapi <c>GET /api/guidance-areas/index</c>.</summary>
    Task<GuidanceIndexDto?> GetGuidanceIndexAsync(CancellationToken cancellationToken = default);
}
