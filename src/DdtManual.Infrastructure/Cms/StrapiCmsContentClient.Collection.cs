using DdtManual.Application.Content;
using Microsoft.Extensions.Logging;

namespace DdtManual.Infrastructure.Cms;

public sealed partial class StrapiCmsContentClient
{
    public async Task<CollectionDetailDto?> GetCollectionBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl) || string.IsNullOrWhiteSpace(slug))
            return null;

        // Same Strapi 5 query as Service Manual CmsApiService.GetCollectionBySlugAsync
        var url =
            "api/collections?filters[slug][$eq]=" + Uri.EscapeDataString(slug.Trim()) +
            "&pagination[pageSize]=1" +
            "&populate[sections][populate][0]=detailed_guides.detailed_guide" +
            "&populate[sections][populate][1]=detailed_guide_pages.detailed_guide_page.detailed_guide" +
            "&populate[sections][populate][2]=external_links.external_link" +
            "&populate[sections][populate][3]=job_descriptions.job_specifications" +
            "&populate[relatedContent][fields][0]=Header&populate[relatedContent][fields][1]=Content" +
            "&populate[applicableProfessions]=true&populate[relatedFiles]=true" +
            "&populate[contentOwner][fields][0]=title&populate[contentOwner][populate][informationPage][fields][0]=urlToRedirectTo";

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CMS returned {Status} for collection '{Slug}'", (int)response.StatusCode, slug);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return StrapiCollectionDetailMapper.Map(json, options.Value.BaseUrl?.Trim().TrimEnd('/'));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Collection request failed for slug '{Slug}'", slug);
            return null;
        }
    }
}
