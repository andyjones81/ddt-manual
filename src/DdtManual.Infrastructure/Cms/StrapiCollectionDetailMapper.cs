using System.Globalization;
using System.Text.Json;
using DdtManual.Application.Content;

namespace DdtManual.Infrastructure.Cms;

/// <summary>Maps Strapi <c>collections</c> API JSON to <see cref="CollectionDetailDto"/> (parity with Service Manual <c>GetCollectionBySlugAsync</c>).</summary>
internal static class StrapiCollectionDetailMapper
{
    public static CollectionDetailDto? Map(string json, string? cmsBaseUrlTrimmed)
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

        if (first.ValueKind == JsonValueKind.Undefined || first.ValueKind == JsonValueKind.Null)
            return null;

        var item = UnwrapAttributes(first);

        var slug = JsonStringProp(item, "slug")?.Trim();
        if (string.IsNullOrEmpty(slug))
            return null;

        var sections = MapSections(item, slug);
        var relatedContent = MapRelatedContent(item);
        var relatedFiles = MapRelatedFiles(item, cmsBaseUrlTrimmed);
        var audience = MapProfessionTags(item);
        var (owner, ownerUrl) = MapContentOwner(item);

        return new CollectionDetailDto
        {
            Title = JsonStringProp(item, "title") ?? slug,
            Slug = slug,
            MetaDescription = JsonStringProp(item, "metaDescription") ?? string.Empty,
            BodyMarkdown = JsonStringProp(item, "body"),
            Sections = sections,
            RelatedContent = relatedContent,
            RelatedFiles = relatedFiles,
            ShowDraftContentBanner = false,
            ShowLastReviewedDateOnPage = JsonBoolProp(item, "showLastReviewedDateOnPage"),
            LastReviewedDateDisplay = FormatDateTime(JsonStringProp(item, "lastReviewedDate")),
            Owner = owner,
            OwnerUrl = ownerUrl,
            AudienceTags = audience,
        };
    }

    private static IReadOnlyList<CollectionSectionDto> MapSections(JsonElement item, string collectionSlug)
    {
        JsonElement? sectionsEl = null;
        foreach (var name in new[] { "collection_sections", "collectionSections", "sections" })
        {
            if (item.TryGetProperty(name, out var s))
            {
                sectionsEl = UnwrapDataArray(s);
                break;
            }
        }

        if (!sectionsEl.HasValue || sectionsEl.Value.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<(CollectionSectionDto Section, int Order, int Index)>();
        var idx = 0;
        foreach (var raw in sectionsEl.Value.EnumerateArray())
        {
            if (raw.ValueKind != JsonValueKind.Object)
                continue;
            var section = UnwrapAttributes(raw);
            var title = JsonStringProp(section, "title") ?? string.Empty;
            var order = 0;
            if (section.TryGetProperty("order", out var o) && o.ValueKind == JsonValueKind.Number)
                order = o.GetInt32();

            var links = MapSectionLinkItems(section, collectionSlug);
            list.Add((new CollectionSectionDto { Title = title, Items = links }, order, idx++));
        }

        return list
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Index)
            .Select(x => x.Section)
            .ToList();
    }

    private static IReadOnlyList<CollectionLinkDto> MapSectionLinkItems(JsonElement section, string collectionSlug)
    {
        if (section.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array && itemsProp.GetArrayLength() > 0)
            return MapFlatSectionItems(itemsProp);

        var withOrder = new List<(CollectionLinkDto Link, int Index)>();
        var index = 0;

        foreach (var name in new[] { "detailed_guides", "detailedGuides" })
        {
            if (!section.TryGetProperty(name, out var arr))
                continue;
            arr = UnwrapDataArrayElement(arr);
            if (arr.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var row in arr.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                    continue;
                if (!row.TryGetProperty("detailed_guide", out var rel) && !row.TryGetProperty("detailedGuide", out rel))
                    continue;
                var attrs = UnwrapStrapiEntity(rel);
                var gSlug = JsonStringProp(attrs, "slug");
                if (string.IsNullOrWhiteSpace(gSlug))
                    continue;
                withOrder.Add((new CollectionLinkDto
                {
                    Title = JsonStringProp(attrs, "title") ?? string.Empty,
                    Url = "/guidance/guides/" + Uri.EscapeDataString(gSlug.Trim()),
                    ContentType = ContentTypeLabel("detailed_guide"),
                    PriorityInGroup = false,
                }, index++));
            }
        }

        foreach (var name in new[] { "detailed_guide_pages", "detailedGuidePages" })
        {
            if (!section.TryGetProperty(name, out var arr))
                continue;
            arr = UnwrapDataArrayElement(arr);
            if (arr.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var row in arr.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                    continue;
                if (!row.TryGetProperty("detailed_guide_page", out var pageRel) &&
                    !row.TryGetProperty("detailedGuidePage", out pageRel))
                    continue;
                var page = UnwrapStrapiEntity(pageRel);
                if (page.ValueKind != JsonValueKind.Object)
                    continue;

                var pageSlug = JsonStringProp(page, "slug");
                if (string.IsNullOrWhiteSpace(pageSlug))
                    continue;

                string url;
                if (page.TryGetProperty("detailed_guide", out var guideRel) || page.TryGetProperty("detailedGuide", out guideRel))
                {
                    var guide = UnwrapStrapiEntity(guideRel);
                    var guideSlug = JsonStringProp(guide, "slug");
                    url = !string.IsNullOrWhiteSpace(guideSlug)
                        ? "/guidance/guides/" + Uri.EscapeDataString(guideSlug.Trim()) + "/" + Uri.EscapeDataString(pageSlug.Trim())
                        : "/guidance/guides/" + Uri.EscapeDataString(pageSlug.Trim());
                }
                else
                    url = "/guidance/guides/" + Uri.EscapeDataString(pageSlug.Trim());

                withOrder.Add((new CollectionLinkDto
                {
                    Title = JsonStringProp(page, "title") ?? string.Empty,
                    Url = url,
                    ContentType = ContentTypeLabel("detailed_guide_page"),
                    PriorityInGroup = false,
                }, index++));
            }
        }

        foreach (var name in new[] { "external_links", "externalLinks" })
        {
            if (!section.TryGetProperty(name, out var arr))
                continue;
            arr = UnwrapDataArrayElement(arr);
            if (arr.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var row in arr.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                    continue;
                if (!row.TryGetProperty("external_link", out var extRel) && !row.TryGetProperty("externalLink", out extRel))
                    continue;
                var ext = UnwrapStrapiEntity(extRel);
                var url = JsonStringProp(ext, "url") ?? string.Empty;
                var priority = JsonBoolProp(ext, "priorityInGroup");
                withOrder.Add((new CollectionLinkDto
                {
                    Title = JsonStringProp(ext, "title") ?? string.Empty,
                    Url = url,
                    OpenInNewTab = JsonBoolProp(ext, "newTab"),
                    LinkType = JsonStringProp(ext, "type"),
                    ContentType = ContentTypeLabel("external_link"),
                    PriorityInGroup = priority,
                }, index++));
            }
        }

        foreach (var name in new[] { "job_descriptions", "jobDescriptions" })
        {
            if (!section.TryGetProperty(name, out var families))
                continue;
            families = UnwrapDataArrayElement(families);
            if (families.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var family in families.EnumerateArray())
            {
                if (family.ValueKind != JsonValueKind.Object)
                    continue;
                var fam = UnwrapAttributes(family);
                if (!fam.TryGetProperty("job_specifications", out var specsEl) &&
                    !fam.TryGetProperty("jobSpecifications", out specsEl))
                    continue;
                specsEl = UnwrapDataArrayElement(specsEl);
                foreach (var specItem in EnumerateRelationEntries(specsEl))
                {
                    var spec = UnwrapStrapiEntity(specItem);
                    var specSlug = JsonStringProp(spec, "slug");
                    if (string.IsNullOrWhiteSpace(specSlug))
                        continue;
                    withOrder.Add((new CollectionLinkDto
                    {
                        Title = JsonStringProp(spec, "title") ?? string.Empty,
                        Url = "/guidance/job-specifications/" + Uri.EscapeDataString(specSlug.Trim()),
                        Grade = JsonStringProp(spec, "grade"),
                        ContentType = ContentTypeLabel("job_specification"),
                        CollectionSlugForQuery = collectionSlug,
                        PriorityInGroup = false,
                    }, index++));
                }
            }
        }

        return withOrder
            .OrderByDescending(x => x.Link.PriorityInGroup)
            .ThenBy(x => x.Index)
            .Select(x => x.Link)
            .ToList();
    }

    private static IEnumerable<JsonElement> EnumerateRelationEntries(JsonElement el)
    {
        if (el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            yield break;

        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in el.EnumerateArray())
            {
                if (x.ValueKind == JsonValueKind.Object)
                    yield return x;
            }
            yield break;
        }

        if (el.ValueKind == JsonValueKind.Object)
            yield return el;
    }

    private static IReadOnlyList<CollectionLinkDto> MapFlatSectionItems(JsonElement itemsArray)
    {
        var tmp = new List<(CollectionLinkDto Link, bool Priority)>();
        foreach (var raw in itemsArray.EnumerateArray())
        {
            if (raw.ValueKind != JsonValueKind.Object)
                continue;
            var el = UnwrapAttributes(raw);
            var type = JsonStringProp(el, "type");
            tmp.Add((new CollectionLinkDto
            {
                Title = JsonStringProp(el, "title") ?? string.Empty,
                Url = JsonStringProp(el, "url") ?? string.Empty,
                ContentType = ContentTypeLabel(type),
                LinkType = JsonStringProp(el, "linkType"),
                OpenInNewTab = JsonBoolProp(el, "newTab"),
                Grade = JsonStringProp(el, "grade"),
                PriorityInGroup = JsonBoolProp(el, "priorityInGroup"),
            }, JsonBoolProp(el, "priorityInGroup")));
        }

        return tmp
            .OrderByDescending(x => x.Priority)
            .Select(x => x.Link)
            .ToList();
    }

    private static IReadOnlyList<CollectionRelatedContentDto> MapRelatedContent(JsonElement item)
    {
        if (!item.TryGetProperty("relatedContent", out var rc) && !item.TryGetProperty("related_content", out rc))
            return [];

        rc = UnwrapDataArrayElement(rc);

        var list = new List<CollectionRelatedContentDto>();
        foreach (var el in EnumerateRelationEntries(rc))
        {
            var block = UnwrapAttributes(el);
            // Strapi component schema uses PascalCase (see cms/components/content/related-content.json).
            var header = JsonStringPropFirst(block, "Header", "header") ?? string.Empty;
            var content = JsonStringPropFirst(block, "Content", "content");
            if (string.IsNullOrWhiteSpace(header) && string.IsNullOrWhiteSpace(content))
                continue;

            list.Add(new CollectionRelatedContentDto
            {
                HeaderMarkdown = header,
                ContentMarkdown = content,
            });
        }

        return list;
    }

    private static IReadOnlyList<CollectionRelatedFileDto> MapRelatedFiles(JsonElement item, string? cmsBase)
    {
        if (!item.TryGetProperty("relatedFiles", out var rf) && !item.TryGetProperty("related_files", out rf))
            return [];

        rf = UnwrapDataArrayElement(rf);
        if (rf.ValueKind != JsonValueKind.Array)
            return [];

        var baseUrl = (cmsBase ?? "").TrimEnd('/');
        var list = new List<CollectionRelatedFileDto>();
        foreach (var el in rf.EnumerateArray())
        {
            var f = UnwrapAttributes(el);
            var url = (JsonStringProp(f, "url") ?? "").Trim();
            if (string.IsNullOrEmpty(url))
                continue;
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(baseUrl))
                url = baseUrl + (url.StartsWith('/') ? url : "/" + url);

            var size = 0m;
            if (f.TryGetProperty("size", out var sz) && sz.ValueKind == JsonValueKind.Number)
                size = sz.GetDecimal();

            list.Add(new CollectionRelatedFileDto
            {
                Name = JsonStringProp(f, "name") ?? "Download",
                Url = url,
                SizeDisplay = FormatFileSize(size),
                FileType = FileTypeFromMimeOrExt(JsonStringProp(f, "mime"), JsonStringProp(f, "ext")),
                Caption = JsonStringProp(f, "caption"),
            });
        }

        return list;
    }

    private static IReadOnlyList<TagRefDto> MapProfessionTags(JsonElement item)
    {
        if (!item.TryGetProperty("applicableProfessions", out var arr) &&
            !item.TryGetProperty("applicable_professions", out arr))
            return [];

        arr = UnwrapDataArrayElement(arr);
        if (arr.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<TagRefDto>();
        foreach (var el in arr.EnumerateArray())
        {
            var t = UnwrapAttributes(el);
            var slug = JsonStringProp(t, "slug") ?? string.Empty;
            var title = JsonStringProp(t, "title") ?? string.Empty;
            if (slug.Length == 0 && title.Length == 0)
                continue;
            list.Add(new TagRefDto { Slug = slug, Title = title });
        }

        return list;
    }

    private static (string? Owner, string? OwnerUrl) MapContentOwner(JsonElement item)
    {
        if (!item.TryGetProperty("contentOwner", out var co) && !item.TryGetProperty("content_owner", out co))
            return (null, null);

        JsonElement ownerEl;
        var uwCo = UnwrapDataSingle(co);
        ownerEl = uwCo.HasValue ? uwCo.Value : co;

        if (ownerEl.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return (null, null);

        var attrs = UnwrapAttributes(ownerEl);
        if (attrs.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return (null, null);

        var title = JsonStringProp(attrs, "title")?.Trim();

        string? redirect = null;
        if (attrs.ValueKind == JsonValueKind.Object &&
            (attrs.TryGetProperty("informationPage", out var ip) || attrs.TryGetProperty("information_page", out ip)))
        {
            if (ip.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return (title, null);

            JsonElement ipEl;
            var uwIp = UnwrapDataSingle(ip);
            ipEl = uwIp.HasValue ? uwIp.Value : ip;

            if (ipEl.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return (title, null);

            var ipAttrs = UnwrapAttributes(ipEl);
            redirect = JsonStringProp(ipAttrs, "urlToRedirectTo") ?? JsonStringProp(ipAttrs, "url_to_redirect_to");
        }

        return (title, redirect?.Trim());
    }

    private static JsonElement UnwrapAttributes(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("attributes", out var a) && a.ValueKind == JsonValueKind.Object)
            return a;
        return el;
    }

    private static JsonElement UnwrapDataArray(JsonElement prop)
    {
        if (prop.ValueKind == JsonValueKind.Object && prop.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array)
            return d;
        return prop;
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
        // JsonElement.Null throws on TryGetProperty — Strapi often omits or nulls relations (e.g. detailed_guide).
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

    /// <summary>First matching property (Strapi REST often emits PascalCase attribute names from the schema).</summary>
    private static string? JsonStringPropFirst(JsonElement obj, params string[] names)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var p))
                continue;
            return p.ValueKind switch
            {
                JsonValueKind.String => p.GetString(),
                JsonValueKind.Number => p.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null,
            };
        }

        return null;
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

    private static string? ContentTypeLabel(string? type) =>
        type switch
        {
            "detailed_guide" => "Guidance",
            "detailed_guide_page" => "Guidance",
            "job_specification" => "Job description",
            "external_link" => "External link",
            _ => null,
        };

    private static string FormatFileSize(decimal bytes)
    {
        if (bytes <= 0) return "0 B";
        string[] units = ["B", "KB", "MB", "GB"];
        var u = 0;
        var n = bytes;
        while (n >= 1024 && u < units.Length - 1)
        {
            n /= 1024;
            u++;
        }

        return u == 0 ? $"{n:F0} {units[u]}" : $"{n:F1} {units[u]}";
    }

    private static string FileTypeFromMimeOrExt(string? mime, string? ext)
    {
        if (!string.IsNullOrWhiteSpace(ext))
            return ext.TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(mime))
            return "File";
        var part = mime.Split('/').LastOrDefault();
        return string.IsNullOrEmpty(part) ? "File" : part.ToUpperInvariant();
    }

    private static string? FormatDateTime(string? iso)
    {
        if (string.IsNullOrEmpty(iso))
            return null;
        if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return dt.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("en-GB"));
        return null;
    }

    internal static IReadOnlyList<CollectionRelatedContentDto> RelatedContentBlocks(JsonElement item) => MapRelatedContent(item);

    internal static IReadOnlyList<CollectionRelatedFileDto> RelatedFileBlocks(JsonElement item, string? cmsBase) =>
        MapRelatedFiles(item, cmsBase);

    internal static (string? Owner, string? OwnerUrl) ContentOwnerTuple(JsonElement item) => MapContentOwner(item);

    internal static string? FormatLastReviewed(string? iso) => FormatDateTime(iso);
}
