using System.Text.Json;
using DdtManual.Application.Content;

namespace DdtManual.Infrastructure.Cms;

/// <summary>Maps Strapi <c>/api/guidance-areas/index</c> JSON to <see cref="GuidanceIndexDto"/>.</summary>
internal static class StrapiGuidanceIndexMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static GuidanceIndexDto? Map(string json)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<IndexEnvelope>(json, JsonOptions);
            if (envelope?.Data == null || envelope.Data.Count == 0)
                return new GuidanceIndexDto { Areas = [] };

            var areas = envelope.Data
                .Where(a => !string.IsNullOrWhiteSpace(a.Slug) && !string.IsNullOrWhiteSpace(a.Name))
                .Select(MapArea)
                .ToList();

            return new GuidanceIndexDto { Areas = areas };
        }
        catch
        {
            return null;
        }
    }

    private static GuidanceAreaGroupDto MapArea(AreaJson a)
    {
        var collections = (a.Collections ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.Slug) && !string.IsNullOrWhiteSpace(c.Title))
            .Select(MapCard)
            .ToList();

        var featured = (a.FeaturedProfessions ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.Slug) || !string.IsNullOrWhiteSpace(t.Title))
            .Select(t => new GuidanceTagRefDto
            {
                Slug = CoalesceSlug(t.Slug, t.Title),
                Title = (t.Title ?? string.Empty).Trim(),
            })
            .ToList();

        return new GuidanceAreaGroupDto
        {
            Name = (a.Name ?? string.Empty).Trim(),
            Slug = (a.Slug ?? string.Empty).Trim(),
            Summary = string.IsNullOrWhiteSpace(a.Summary) ? null : a.Summary.Trim(),
            Description = string.IsNullOrWhiteSpace(a.Description) ? null : a.Description.Trim(),
            ColourHex = NormaliseHex(a.ColourHex),
            FeaturedProfessions = featured,
            Collections = collections,
        };
    }

    private static GuidanceCollectionCardDto MapCard(CardJson c)
    {
        var slug = (c.Slug ?? string.Empty).Trim();
        var contentType = string.IsNullOrWhiteSpace(c.ContentType)
            ? "Collection"
            : c.ContentType.Trim();

        var alsoIn = (c.AlsoInAreas ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Slug) && !string.IsNullOrWhiteSpace(x.Title))
            .Select(x => new GuidanceAreaRefDto
            {
                Title = (x.Title ?? string.Empty).Trim(),
                Slug = (x.Slug ?? string.Empty).Trim(),
            })
            .ToList();

        var professions = (c.ApplicableProfessions ?? [])
            .Where(t => !string.IsNullOrWhiteSpace(t.Slug) || !string.IsNullOrWhiteSpace(t.Title))
            .Select(t => new GuidanceTagRefDto
            {
                Slug = CoalesceSlug(t.Slug, t.Title),
                Title = (t.Title ?? string.Empty).Trim(),
            })
            .ToList();

        var description = string.IsNullOrWhiteSpace(c.Description)
            ? (c.Title ?? string.Empty).Trim()
            : c.Description.Trim();

        return new GuidanceCollectionCardDto
        {
            Title = (c.Title ?? string.Empty).Trim(),
            Slug = slug,
            Url = RewriteCardUrl(c.Url, slug, contentType),
            ContentType = contentType,
            Description = description,
            ItemCount = c.ItemCount,
            Featured = c.Featured,
            Tags = (c.Tags ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList(),
            ApplicableProfessions = professions,
            AlsoInAreas = alsoIn,
        };
    }

    /// <summary>Service Manual emits <c>/guidance/collections/{slug}</c>; DdT Manual uses <c>/collection/{slug}</c>.</summary>
    internal static string RewriteCardUrl(string? url, string slug, string contentType)
    {
        var u = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        if (!string.IsNullOrEmpty(u))
        {
            if (u.StartsWith("/guidance/collections/", StringComparison.OrdinalIgnoreCase))
            {
                var rest = u["/guidance/collections/".Length..].Trim('/');
                if (string.IsNullOrEmpty(rest))
                    rest = slug;
                return "/collection/" + Uri.EscapeDataString(rest);
            }

            return u;
        }

        if (contentType.Equals("Detailed guide", StringComparison.OrdinalIgnoreCase))
            return "/guidance/guides/" + Uri.EscapeDataString(slug);

        return "/collection/" + Uri.EscapeDataString(slug);
    }

    private static string CoalesceSlug(string? slug, string? title)
    {
        if (!string.IsNullOrWhiteSpace(slug))
            return slug.Trim();
        return GenerateId(title ?? string.Empty);
    }

    private static string GenerateId(string text)
    {
        var id = text.ToLowerInvariant();
        id = System.Text.RegularExpressions.Regex.Replace(id, @"[^\w\s-]", "");
        id = System.Text.RegularExpressions.Regex.Replace(id, @"\s+", "-");
        id = System.Text.RegularExpressions.Regex.Replace(id, @"-+", "-");
        return id.Trim('-');
    }

    private static string NormaliseHex(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
            return "#1d70b8";
        return trimmed.StartsWith('#') ? trimmed : "#" + trimmed;
    }

    private sealed class IndexEnvelope
    {
        public List<AreaJson>? Data { get; set; }
    }

    private sealed class AreaJson
    {
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? Summary { get; set; }
        public string? Description { get; set; }
        public string? ColourHex { get; set; }
        public List<TagJson>? FeaturedProfessions { get; set; }
        public List<CardJson>? Collections { get; set; }
    }

    private sealed class TagJson
    {
        public string? Slug { get; set; }
        public string? Title { get; set; }
    }

    private sealed class CardJson
    {
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? Url { get; set; }
        public string? ContentType { get; set; }
        public string? Description { get; set; }
        public int ItemCount { get; set; }
        public bool Featured { get; set; }
        public List<string>? Tags { get; set; }
        public List<TagJson>? ApplicableProfessions { get; set; }
        public List<AreaRefJson>? AlsoInAreas { get; set; }
    }

    private sealed class AreaRefJson
    {
        public string? Title { get; set; }
        public string? Slug { get; set; }
    }
}
