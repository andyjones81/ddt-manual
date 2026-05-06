using System.Text;
using System.Text.Json;
using DdtManual.Application.Content;

namespace DdtManual.Infrastructure.Cms;

/// <summary>Maps Strapi detailed-guide and detailed-guide-pages JSON to application DTOs.</summary>
internal static class StrapiDetailedGuideMapper
{
    public static DetailedGuideOverviewDto? MapOverview(string json, string? cmsBaseUrlTrimmed)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var dataEl))
            return null;

        JsonElement first;
        if (dataEl.ValueKind == JsonValueKind.Array)
            first = dataEl.EnumerateArray().FirstOrDefault();
        else if (dataEl.ValueKind == JsonValueKind.Object)
            first = dataEl;
        else
            return null;

        if (first.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;

        var item = UnwrapAttributes(first);
        var slug = JsonStringProp(item, "slug")?.Trim();
        if (string.IsNullOrEmpty(slug))
            return null;

        var pages = MapPageSummaries(item);
        var (collSlug, collTitle, collList) = MapCollections(item);
        var (owner, ownerUrl) = StrapiCollectionDetailMapper.ContentOwnerTuple(item);

        return new DetailedGuideOverviewDto
        {
            Title = JsonStringProp(item, "title") ?? slug,
            Slug = slug,
            MetaDescription = JsonStringProp(item, "metaDescription"),
            BodyMarkdown = StrapiRichTextMarkdown.FromJsonElement(item, "body") ?? JsonStringProp(item, "body"),
            OverrideOverviewTitle = JsonStringProp(item, "overrideOverviewTitle"),
            HideContentsOnPrimaryPage = JsonBoolProp(item, "hideContentsOnPrimaryPage"),
            ShowGuidePagesOnRight = JsonBoolProp(item, "showGuidePagesOnRight"),
            Pages = pages,
            CollectionSlug = collSlug,
            CollectionTitle = collTitle,
            Collections = collList,
            RelatedContent = StrapiCollectionDetailMapper.RelatedContentBlocks(item),
            RelatedFiles = StrapiCollectionDetailMapper.RelatedFileBlocks(item, cmsBaseUrlTrimmed),
            ShowLastReviewedDateOnPage = JsonBoolProp(item, "showLastReviewedDateOnPage"),
            LastReviewedDateDisplay = StrapiCollectionDetailMapper.FormatLastReviewed(JsonStringProp(item, "lastReviewedDate")),
            ShowOwnerOnPage = !item.TryGetProperty("showOwnerOnPage", out _) ||
                               JsonBoolProp(item, "showOwnerOnPage"),
            Owner = owner,
            OwnerUrl = ownerUrl,
            CustomCss = JsonStringProp(item, "customCSS") ?? JsonStringProp(item, "customCss"),
            CustomJs = JsonStringProp(item, "customJS") ?? JsonStringProp(item, "customJs"),
            ShowDraftContentBanner = false,
            ApplicableProfessions = MapApplicableProfessionLabels(item),
            ApplicablePhases = MapApplicablePhaseTags(item),
        };
    }

    /// <summary>Find Strapi documentId for a guide page whose parent guide slug matches.</summary>
    public static string? FindGuidePageDocumentId(string findJson, string guideSlug, string pageSlug)
    {
        using var doc = JsonDocument.Parse(findJson);
        if (!doc.RootElement.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var row in dataEl.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                continue;
            // Slug may be absent if the REST query used a sparse fieldset without `slug`; the list is already
            // filtered by slug, so only compare when both sides are present.
            var pageSlugActual = JsonStringProp(UnwrapAttributes(row), "slug")?.Trim();
            if (pageSlugActual != null
                && !string.Equals(pageSlugActual, pageSlug, StringComparison.OrdinalIgnoreCase))
                continue;

            var docId = JsonStringProp(row, "documentId") ?? JsonStringProp(UnwrapAttributes(row), "documentId");
            if (string.IsNullOrEmpty(docId))
                continue;

            if (!row.TryGetProperty("detailed_guide", out var dgRel) && !row.TryGetProperty("detailedGuide", out dgRel))
                continue;

            var dg = UnwrapStrapiEntity(dgRel);
            if (dg.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                continue;
            dg = UnwrapAttributes(dg);
            var gSlug = JsonStringProp(dg, "slug")?.Trim();
            if (string.Equals(gSlug, guideSlug, StringComparison.OrdinalIgnoreCase))
                return docId;
        }

        return null;
    }

    public static DetailedGuideChildPageDto? MapChildPageDetail(string json, string? cmsBaseUrlTrimmed)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var dataEl))
            return null;

        if (dataEl.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        var item = UnwrapAttributes(dataEl);
        var pageSlug = JsonStringProp(item, "slug")?.Trim();
        if (string.IsNullOrEmpty(pageSlug))
            return null;

        JsonElement? dgEl = null;
        if (item.TryGetProperty("detailed_guide", out var d1))
            dgEl = d1;
        else if (item.TryGetProperty("detailedGuide", out var d2))
            dgEl = d2;

        JsonElement guide = default;
        var hasGuide = false;
        if (dgEl.HasValue)
        {
            var ug = UnwrapStrapiEntity(dgEl.Value);
            if (ug.ValueKind == JsonValueKind.Object)
            {
                guide = UnwrapAttributes(ug);
                hasGuide = true;
            }
        }

        var siblingPages = hasGuide ? MapPageSummaries(guide) : [];

        string? collSlug;
        string? collTitle;
        IReadOnlyList<CollectionRefDto> collList;
        if (hasGuide)
            (collSlug, collTitle, collList) = MapCollections(guide);
        else
        {
            collSlug = null;
            collTitle = null;
            collList = [];
        }

        var (owner, ownerUrl) = hasGuide ? StrapiCollectionDetailMapper.ContentOwnerTuple(guide) : StrapiCollectionDetailMapper.ContentOwnerTuple(item);

        var bodyMarkdown = StrapiRichTextMarkdown.FromJsonElement(item, "body") ?? JsonStringProp(item, "body");
        bodyMarkdown = AppendDetailedGuidePageSectionBodies(bodyMarkdown, item);

        return new DetailedGuideChildPageDto
        {
            PageTitle = JsonStringProp(item, "title") ?? pageSlug,
            PageSlug = pageSlug,
            MetaDescription = JsonStringProp(item, "metaDescription"),
            BodyMarkdown = bodyMarkdown,
            BeforeContentsMarkdown =
                StrapiRichTextMarkdown.FromJsonElement(item, "beforeContents") ?? JsonStringProp(item, "beforeContents"),
            HideTitleAndDescription = JsonBoolProp(item, "hideTitleAndDescription"),
            HideContentsNav = JsonBoolProp(item, "hideContents"),
            HideGuidePagesNav = JsonBoolProp(item, "hideGuidePagesNav"),
            GuideTitle = hasGuide ? (JsonStringProp(guide, "title") ?? string.Empty) : string.Empty,
            GuideSlug = hasGuide ? (JsonStringProp(guide, "slug") ?? string.Empty) : string.Empty,
            GuideMetaDescription = hasGuide ? JsonStringProp(guide, "metaDescription") : null,
            OverrideOverviewTitle = hasGuide ? JsonStringProp(guide, "overrideOverviewTitle") : null,
            HideContentsOnPrimaryPage = hasGuide && JsonBoolProp(guide, "hideContentsOnPrimaryPage"),
            ShowGuidePagesOnRight = hasGuide && JsonBoolProp(guide, "showGuidePagesOnRight"),
            CollectionSlug = collSlug,
            CollectionTitle = collTitle,
            Collections = collList,
            SiblingPages = siblingPages,
            RelatedContent = StrapiCollectionDetailMapper.RelatedContentBlocks(item),
            RelatedFiles = StrapiCollectionDetailMapper.RelatedFileBlocks(item, cmsBaseUrlTrimmed),
            // Match overview masthead meta: use guide-level review/owner/collection when populated from CMS.
            ShowLastReviewedDateOnPage = hasGuide
                ? (!guide.TryGetProperty("showLastReviewedDateOnPage", out _) ||
                   JsonBoolProp(guide, "showLastReviewedDateOnPage"))
                : JsonBoolProp(item, "showLastReviewedDateOnPage"),
            LastReviewedDateDisplay = hasGuide
                ? StrapiCollectionDetailMapper.FormatLastReviewed(JsonStringProp(guide, "lastReviewedDate"))
                : StrapiCollectionDetailMapper.FormatLastReviewed(JsonStringProp(item, "lastReviewedDate")),
            ShowOwnerOnPage = !hasGuide || !guide.TryGetProperty("showOwnerOnPage", out _) ||
                               JsonBoolProp(guide, "showOwnerOnPage"),
            Owner = owner,
            OwnerUrl = ownerUrl,
            CustomCss = hasGuide ? (JsonStringProp(guide, "customCSS") ?? JsonStringProp(guide, "customCss")) : null,
            CustomJs = hasGuide ? (JsonStringProp(guide, "customJS") ?? JsonStringProp(guide, "customJs")) : null,
            ShowDraftContentBanner = false,
            ApplicableProfessions = MapApplicableProfessionLabels(item),
            ApplicablePhases = MapApplicablePhaseTags(item),
        };
    }

    /// <summary>
    /// Concatenates repeatable <c>Section</c> component bodies (richtext) after the main <c>body</c> field.
    /// Editors often place <c>[[ServiceStandardList]]</c> in a module section rather than the root body.
    /// </summary>
    private static string? AppendDetailedGuidePageSectionBodies(string? baseMarkdown, JsonElement pageItem)
    {
        JsonElement sectionsProp = default;
        var found = false;
        foreach (var name in new[] { "Section", "section" })
        {
            if (!pageItem.TryGetProperty(name, out sectionsProp))
                continue;
            found = true;
            break;
        }

        if (!found)
            return baseMarkdown;

        var arr = UnwrapDataArrayElement(sectionsProp);
        if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0)
            return baseMarkdown;

        var sb = new StringBuilder(baseMarkdown ?? "");
        foreach (var rawComp in arr.EnumerateArray())
        {
            if (rawComp.ValueKind != JsonValueKind.Object)
                continue;
            var c = UnwrapAttributes(rawComp);
            var sectionMd = StrapiRichTextMarkdown.FromJsonElement(c, "body");
            if (string.IsNullOrWhiteSpace(sectionMd))
                continue;
            if (sb.Length > 0)
                sb.Append("\n\n");
            sb.Append(sectionMd);
        }

        return sb.Length == 0 ? baseMarkdown : sb.ToString();
    }

    private static IReadOnlyList<DetailedGuidePageSummaryDto> MapPageSummaries(JsonElement guideItem)
    {
        JsonElement? pagesProp = null;
        foreach (var name in new[] { "detailed_guide_pages", "detailedGuidePages" })
        {
            if (guideItem.TryGetProperty(name, out var p))
            {
                pagesProp = p;
                break;
            }
        }

        if (!pagesProp.HasValue)
            return [];

        var arr = UnwrapDataArrayElement(pagesProp.Value);
        if (arr.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<DetailedGuidePageSummaryDto>();
        foreach (var raw in arr.EnumerateArray())
        {
            if (raw.ValueKind != JsonValueKind.Object)
                continue;
            var p = UnwrapAttributes(raw);
            var ps = JsonStringProp(p, "slug")?.Trim();
            var title = JsonStringProp(p, "title")?.Trim();
            if (string.IsNullOrEmpty(ps))
                continue;
            list.Add(new DetailedGuidePageSummaryDto { Slug = ps, Title = title ?? ps });
        }

        return list;
    }

    private static IReadOnlyList<string> MapApplicableProfessionLabels(JsonElement item)
    {
        var list = new List<string>();
        foreach (var propName in new[] { "applicableProfessions", "applicable_professions" })
        {
            if (!item.TryGetProperty(propName, out var rel))
                continue;

            var arr = UnwrapDataArrayElement(rel);
            if (arr.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var raw in arr.EnumerateArray())
            {
                if (raw.ValueKind != JsonValueKind.Object)
                    continue;
                var p = UnwrapAttributes(raw);
                var label = JsonStringProp(p, "plural")?.Trim();
                if (string.IsNullOrEmpty(label))
                    label = JsonStringProp(p, "title")?.Trim();
                if (string.IsNullOrWhiteSpace(label))
                    continue;
                list.Add(label);
            }

            if (list.Count > 0)
                return list;
        }

        return [];
    }

    private static IReadOnlyList<ApplicablePhaseTagDto> MapApplicablePhaseTags(JsonElement item)
    {
        var list = new List<ApplicablePhaseTagDto>();
        foreach (var propName in new[] { "applicablePhases", "applicable_phases" })
        {
            if (!item.TryGetProperty(propName, out var rel))
                continue;

            var arr = UnwrapDataArrayElement(rel);
            if (arr.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var raw in arr.EnumerateArray())
            {
                if (raw.ValueKind != JsonValueKind.Object)
                    continue;
                var p = UnwrapAttributes(raw);
                var slug = JsonStringProp(p, "slug")?.Trim() ?? "";
                var title = JsonStringProp(p, "title")?.Trim();
                if (string.IsNullOrEmpty(title))
                    continue;
                list.Add(new ApplicablePhaseTagDto(slug, title));
            }

            if (list.Count > 0)
                return list;
        }

        return [];
    }

    private static (string? Slug, string? Title, IReadOnlyList<CollectionRefDto> List) MapCollections(JsonElement item)
    {
        if (!item.TryGetProperty("collection", out var col))
            return (null, null, []);

        var uw = UnwrapDataSingle(col);
        var el = uw.HasValue ? uw.Value : col;
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return (null, null, []);

        var c = UnwrapAttributes(el);
        var slug = JsonStringProp(c, "slug")?.Trim();
        var title = JsonStringProp(c, "title")?.Trim();
        if (string.IsNullOrEmpty(slug) || string.IsNullOrEmpty(title))
            return (slug, title, []);

        return (slug, title, [new CollectionRefDto { Slug = slug, Title = title }]);
    }

    private static JsonElement UnwrapAttributes(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("attributes", out var a) && a.ValueKind == JsonValueKind.Object)
            return a;
        return el;
    }

    private static JsonElement UnwrapDataArrayElement(JsonElement prop)
    {
        if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("data", out var d))
            return d;
        return prop;
    }

    private static JsonElement? UnwrapDataSingle(JsonElement rel)
    {
        if (rel.ValueKind == JsonValueKind.Null)
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

    private static JsonElement UnwrapStrapiEntity(JsonElement el)
    {
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return el;

        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("data", out var data))
        {
            if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return data;

            if (data.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
                return attrs;
            return data.ValueKind == JsonValueKind.Object ? data : el;
        }

        if (el.ValueKind == JsonValueKind.Object &&
            el.TryGetProperty("attributes", out var topAttrs) &&
            topAttrs.ValueKind == JsonValueKind.Object)
            return topAttrs;

        return el;
    }

    private static string? JsonStringProp(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static bool JsonBoolProp(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var p))
            return false;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => string.Equals(p.GetString(), "true", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }
}
