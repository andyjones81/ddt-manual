using System.Runtime.CompilerServices;
using System.Text.Json;
using DdtManual.Application.Content;
using Microsoft.Extensions.Logging;

namespace DdtManual.Infrastructure.Cms;

/// <summary>
/// Fetches the same published Strapi collections as Service Manual <c>GetAllPublishedContentAsync</c>
/// (articles, collections, detailed guides, detailed guide pages, roadmap single type).
/// </summary>
internal static class PublishedContentIndexAggregator
{
    private const int PageSize = 250;

    public static async Task AggregateAsync(
        HttpClient client,
        ILogger logger,
        List<ContentIndexItemDto> items,
        CancellationToken cancellationToken)
    {
        await AddArticlesAsync(client, logger, items, cancellationToken).ConfigureAwait(false);
        await AddCollectionsAsync(client, logger, items, cancellationToken).ConfigureAwait(false);
        await AddDetailedGuidesAsync(client, logger, items, cancellationToken).ConfigureAwait(false);
        await AddDetailedGuidePagesAsync(client, logger, items, cancellationToken).ConfigureAwait(false);
        await AddRoadmapAsync(client, logger, items, cancellationToken).ConfigureAwait(false);

        if (items.Count == 0)
        {
            logger.LogWarning(
                "Content index returned 0 items. Check Cms:BaseUrl, Cms:ApiToken, and Strapi find permissions for articles, collections, detailed-guides, detailed-guide-pages, roadmap.");
        }
    }

    private static async Task AddArticlesAsync(HttpClient client, ILogger logger, List<ContentIndexItemDto> items, CancellationToken ct)
    {
        var url =
            "api/articles?pagination[pageSize]=" + PageSize +
            "&fields[0]=title&fields[1]=metaDescription&fields[2]=slug";

        await foreach (var row in ReadCollectionRowsAsync(client, logger, url, "api/articles", ct).ConfigureAwait(false))
        {
            var slug = GetString(row, "slug")?.Trim();
            if (string.IsNullOrEmpty(slug))
                continue;

            items.Add(new ContentIndexItemDto
            {
                Title = GetString(row, "title") ?? slug,
                MetaDescription = GetString(row, "metaDescription"),
                ContentType = "Article",
                Url = "/article/" + Uri.EscapeDataString(slug),
                Slug = slug,
            });
        }
    }

    private static async Task AddCollectionsAsync(HttpClient client, ILogger logger, List<ContentIndexItemDto> items, CancellationToken ct)
    {
        var url =
            "api/collections?pagination[pageSize]=" + PageSize +
            "&fields[0]=title&fields[1]=metaDescription&fields[2]=slug" +
            "&populate[applicablePhases][fields][0]=slug&populate[applicablePhases][fields][1]=title" +
            "&populate[applicableProfessions][fields][0]=slug&populate[applicableProfessions][fields][1]=title";

        await foreach (var row in ReadCollectionRowsAsync(client, logger, url, "api/collections", ct).ConfigureAwait(false))
        {
            var slug = GetString(row, "slug")?.Trim();
            if (string.IsNullOrEmpty(slug))
                continue;

            items.Add(new ContentIndexItemDto
            {
                Title = GetString(row, "title") ?? slug,
                MetaDescription = GetString(row, "metaDescription"),
                ContentType = "Collection",
                Url = "/collection/" + Uri.EscapeDataString(slug),
                Slug = slug,
                ApplicablePhaseTags = ReadTagRefs(row, "applicablePhases", "applicable_phases"),
                ApplicableProfessionTags = ReadTagRefs(row, "applicableProfessions", "applicable_professions"),
            });
        }
    }

    private static async Task AddDetailedGuidesAsync(HttpClient client, ILogger logger, List<ContentIndexItemDto> items, CancellationToken ct)
    {
        var url =
            "api/detailed-guides?pagination[pageSize]=" + PageSize +
            "&fields[0]=title&fields[1]=metaDescription&fields[2]=slug" +
            "&populate[collection][fields][0]=title&populate[collection][fields][1]=slug" +
            "&populate[applicableProfessions][fields][0]=slug&populate[applicableProfessions][fields][1]=title";

        await foreach (var row in ReadCollectionRowsAsync(client, logger, url, "api/detailed-guides", ct).ConfigureAwait(false))
        {
            var slug = GetString(row, "slug")?.Trim();
            if (string.IsNullOrEmpty(slug))
                continue;

            var coll = FirstRelationObject(row, "collection");
            string? collTitle = null, collSlug = null;
            if (coll.HasValue)
            {
                var c = UnwrapAttributes(coll.Value);
                collTitle = GetString(c, "title");
                collSlug = GetString(c, "slug");
            }

            items.Add(new ContentIndexItemDto
            {
                Title = GetString(row, "title") ?? slug,
                MetaDescription = GetString(row, "metaDescription"),
                ContentType = "Detailed Guide",
                Url = "/guidance/guides/" + Uri.EscapeDataString(slug),
                Slug = slug,
                CollectionTitle = collTitle,
                CollectionSlug = collSlug,
                ApplicableProfessionTags = ReadTagRefs(row, "applicableProfessions", "applicable_professions"),
            });
        }
    }

    private static async Task AddDetailedGuidePagesAsync(HttpClient client, ILogger logger, List<ContentIndexItemDto> items, CancellationToken ct)
    {
        var url =
            "api/detailed-guide-pages?pagination[pageSize]=" + PageSize +
            "&fields[0]=title&fields[1]=metaDescription&fields[2]=slug" +
            "&populate[detailed_guide][fields][0]=slug" +
            "&populate[detailed_guide][populate][collection][fields][0]=title&populate[detailed_guide][populate][collection][fields][1]=slug" +
            "&populate[applicablePhases][fields][0]=slug&populate[applicablePhases][fields][1]=title" +
            "&populate[applicableProfessions][fields][0]=slug&populate[applicableProfessions][fields][1]=title";

        await foreach (var row in ReadCollectionRowsAsync(client, logger, url, "api/detailed-guide-pages", ct).ConfigureAwait(false))
        {
            var pageSlug = GetString(row, "slug")?.Trim();
            var dg = FirstRelationObject(row, "detailed_guide") ?? FirstRelationObject(row, "detailedGuide");
            if (!dg.HasValue || string.IsNullOrEmpty(pageSlug))
                continue;

            var guide = UnwrapAttributes(dg.Value);
            var guideSlug = GetString(guide, "slug")?.Trim();
            if (string.IsNullOrEmpty(guideSlug))
                continue;

            JsonElement? collEl = null;
            if (guide.TryGetProperty("collection", out var cDirect))
                collEl = UnwrapDataSingle(cDirect);
            else if (guide.TryGetProperty("Collection", out var cLegacy))
                collEl = UnwrapDataSingle(cLegacy);

            string? collTitle = null, collSlug = null;
            if (collEl.HasValue)
            {
                var c = UnwrapAttributes(collEl.Value);
                collTitle = GetString(c, "title");
                collSlug = GetString(c, "slug");
            }

            items.Add(new ContentIndexItemDto
            {
                Title = GetString(row, "title") ?? pageSlug,
                MetaDescription = GetString(row, "metaDescription"),
                ContentType = "Detailed Guide Page",
                Url = "/guidance/guides/" + Uri.EscapeDataString(guideSlug) + "/" + Uri.EscapeDataString(pageSlug),
                ParentContentType = "Detailed Guide",
                ParentSlug = guideSlug,
                CollectionTitle = collTitle,
                CollectionSlug = collSlug,
                ApplicablePhaseTags = ReadTagRefs(row, "applicablePhases", "applicable_phases"),
                ApplicableProfessionTags = ReadTagRefs(row, "applicableProfessions", "applicable_professions"),
            });
        }
    }

    private static async Task AddRoadmapAsync(HttpClient client, ILogger logger, List<ContentIndexItemDto> items, CancellationToken ct)
    {
        try
        {
            const string url = "api/roadmap?fields[0]=title&fields[1]=metaDescription&fields[2]=body&fields[3]=updateHistory";
            var response = await client.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("CMS returned {Status} for roadmap single type", (int)response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return;

            var row = UnwrapAttributes(data);
            var title = GetString(row, "title");
            if (string.IsNullOrEmpty(title))
                return;

            items.Add(new ContentIndexItemDto
            {
                Title = title,
                MetaDescription = GetString(row, "metaDescription"),
                ContentType = "Roadmap",
                Url = "/roadmap",
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error fetching roadmap for content index");
        }
    }

    private static async IAsyncEnumerable<JsonElement> ReadCollectionRowsAsync(
        HttpClient client,
        ILogger logger,
        string url,
        string endpointLabel,
        [EnumeratorCancellation] CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(url, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Request failed for content index endpoint {Endpoint}", endpointLabel);
            yield break;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            logger.LogWarning(
                "Content index endpoint {Endpoint} returned {Status}. Body: {Body}",
                endpointLabel,
                (int)response.StatusCode,
                body.Length > 240 ? body[..240] + "…" : body);
            yield break;
        }

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Invalid JSON from {Endpoint}", endpointLabel);
            yield break;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                yield break;

            foreach (var el in data.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Null)
                    continue;
                yield return UnwrapAttributes(el);
            }
        }
    }

    private static JsonElement UnwrapAttributes(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
            return attrs;
        return el;
    }

    private static JsonElement? FirstRelationObject(JsonElement row, string camelName)
    {
        if (!row.TryGetProperty(camelName, out var rel))
            return null;
        var single = UnwrapDataSingle(rel);
        return single;
    }

    private static JsonElement? UnwrapDataSingle(JsonElement rel)
    {
        if (rel.ValueKind == JsonValueKind.Null || rel.ValueKind == JsonValueKind.Undefined)
            return null;

        if (rel.ValueKind == JsonValueKind.Object && rel.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Object)
                return data;
            if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
                return data[0];
        }

        return rel.ValueKind == JsonValueKind.Object ? rel : null;
    }

    private static string? GetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Null => null,
            _ => null,
        };
    }

    private static List<TagRefDto> ReadTagRefs(JsonElement obj, params string[] propertyNames)
    {
        JsonElement? arrEl = null;
        foreach (var name in propertyNames)
        {
            if (!obj.TryGetProperty(name, out var prop))
                continue;
            if (prop.ValueKind == JsonValueKind.Null)
                continue;

            if (prop.ValueKind == JsonValueKind.Array)
            {
                arrEl = prop;
                break;
            }

            if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("data", out var data))
            {
                if (data.ValueKind == JsonValueKind.Array)
                {
                    arrEl = data;
                    break;
                }
            }
        }

        if (!arrEl.HasValue)
            return [];

        var list = new List<TagRefDto>();
        foreach (var item in arrEl.Value.EnumerateArray())
        {
            var flat = UnwrapAttributes(item);
            var slug = GetString(flat, "slug") ?? string.Empty;
            var title = GetString(flat, "title") ?? string.Empty;
            if (slug.Length == 0 && title.Length == 0)
                continue;
            list.Add(new TagRefDto { Slug = slug, Title = title });
        }

        return list;
    }
}
