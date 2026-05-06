using DdtManual.Application.Content;
using Microsoft.Extensions.Logging;

namespace DdtManual.Infrastructure.Cms;

public sealed partial class StrapiCmsContentClient
{
    public async Task<DetailedGuideOverviewDto?> GetDetailedGuideBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl) || string.IsNullOrWhiteSpace(slug))
            return null;

        var url =
            "api/detailed-guides?filters[slug][$eq]=" + Uri.EscapeDataString(slug.Trim()) +
            "&pagination[pageSize]=1" +
            "&fields[0]=title&fields[1]=slug&fields[2]=metaDescription&fields[3]=body&fields[4]=showLastReviewedDateOnPage&fields[5]=lastReviewedDate&fields[6]=hideContentsOnPrimaryPage&fields[7]=showOwnerOnPage&fields[8]=showApplicablePhasesOnPage&fields[9]=showApplicableProfessionsOnPage&fields[10]=customJS&fields[11]=customCSS&fields[12]=overrideOverviewTitle&fields[13]=showGuidePagesOnRight&fields[14]=publishedAt" +
            "&populate[detailed_guide_pages][fields][0]=title&populate[detailed_guide_pages][fields][1]=slug&populate[detailed_guide_pages][fields][2]=metaDescription" +
            "&populate[detailed_guide_pages][populate][applicablePhases][fields][0]=title&populate[detailed_guide_pages][populate][applicablePhases][fields][1]=slug" +
            "&populate[detailed_guide_pages][populate][applicableProfessions][fields][0]=title&populate[detailed_guide_pages][populate][applicableProfessions][fields][1]=slug&populate[detailed_guide_pages][populate][applicableProfessions][fields][2]=plural" +
            "&populate[collection][fields][0]=title&populate[collection][fields][1]=slug" +
            "&populate[contentOwner][fields][0]=title&populate[contentOwner][populate][informationPage][fields][0]=urlToRedirectTo" +
            "&populate[relatedContent][fields][0]=Header&populate[relatedContent][fields][1]=Content" +
            "&populate[applicablePhases][fields][0]=title&populate[applicablePhases][fields][1]=slug" +
            "&populate[applicableProfessions][fields][0]=title&populate[applicableProfessions][fields][1]=slug&populate[applicableProfessions][fields][2]=plural" +
            "&populate[relatedFiles]=true";

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CMS returned {Status} for detailed guide '{Slug}'", (int)response.StatusCode, slug);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return StrapiDetailedGuideMapper.MapOverview(json, options.Value.BaseUrl?.Trim().TrimEnd('/'));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Detailed guide request failed for slug '{Slug}'", slug);
            return null;
        }
    }

    public async Task<DetailedGuideChildPageDto?> GetDetailedGuidePageBySlugAsync(
        string guideSlug,
        string pageSlug,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Value.BaseUrl) ||
            string.IsNullOrWhiteSpace(guideSlug) ||
            string.IsNullOrWhiteSpace(pageSlug))
            return null;

        // Must request `slug` alongside documentId: sparse fieldsets omit undeclared fields, so without `slug`
        // the mapper cannot match rows and every guide child page 404s.
        var findUrl =
            "api/detailed-guide-pages?filters[slug][$eq]=" + Uri.EscapeDataString(pageSlug.Trim()) +
            "&pagination[pageSize]=100" +
            "&fields[0]=documentId&fields[1]=slug" +
            "&populate[detailed_guide][fields][0]=slug";

        var client = httpClientFactory.CreateClient(HttpClientName);
        try
        {
            var findResponse = await client.GetAsync(findUrl, cancellationToken).ConfigureAwait(false);
            if (!findResponse.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "CMS returned {Status} when resolving guide page candidates for '{PageSlug}'",
                    (int)findResponse.StatusCode,
                    pageSlug);
                return null;
            }

            var findJson = await findResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var documentId = StrapiDetailedGuideMapper.FindGuidePageDocumentId(findJson, guideSlug, pageSlug);
            if (string.IsNullOrEmpty(documentId))
                return null;

            var detailUrl = "api/detailed-guide-pages/" + Uri.EscapeDataString(documentId) + "?populate=*";
            var response = await client.GetAsync(detailUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "CMS returned {Status} for guide page document '{DocumentId}' (slug '{PageSlug}')",
                    (int)response.StatusCode,
                    documentId,
                    pageSlug);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var dto = StrapiDetailedGuideMapper.MapChildPageDetail(json, options.Value.BaseUrl?.Trim().TrimEnd('/'));
            if (dto == null)
                return null;

            var overview = await GetDetailedGuideBySlugAsync(guideSlug, cancellationToken).ConfigureAwait(false);
            var siblings = overview?.Pages is { Count: > 0 } op ? op : dto.SiblingPages;
            return dto with { SiblingPages = siblings };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Detailed guide page request failed for '{GuideSlug}/{PageSlug}'", guideSlug, pageSlug);
            return null;
        }
    }
}
