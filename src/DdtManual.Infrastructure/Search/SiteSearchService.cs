using System.Text.Json;
using DdtManual.Application.Abstractions;
using DdtManual.Application.Content;
using DdtManual.Infrastructure.Standards;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DdtManual.Infrastructure.Search;

/// <summary>
/// Searches CMS published index and DDT standards with the same scoring as Service Manual <c>SearchService</c>.
/// Collection links use DdT routes (<c>/collection/{slug}</c>).
/// </summary>
public sealed class SiteSearchService : ISearchService
{
    public const string StandardsHttpClientName = "StandardsCMS";

    private const int ScoreTitleExact = 100;
    private const int ScoreTitleContains = 80;
    private const int ScoreMetaExact = 50;
    private const int ScoreMetaContains = 30;

    private readonly ICmsContentClient _cmsContentClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<StandardsCmsOptions> _standardsOptions;
    private readonly ILogger<SiteSearchService> _logger;

    public SiteSearchService(
        ICmsContentClient cmsContentClient,
        IHttpClientFactory httpClientFactory,
        IOptions<StandardsCmsOptions> standardsOptions,
        ILogger<SiteSearchService> logger)
    {
        _cmsContentClient = cmsContentClient;
        _httpClientFactory = httpClientFactory;
        _standardsOptions = standardsOptions;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(
        string? keywords,
        IReadOnlyList<string>? types = null,
        CancellationToken cancellationToken = default)
    {
        var query = keywords?.Trim();
        if (string.IsNullOrEmpty(query))
            return [];

        var terms = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0)
            return [];

        var results = new List<(SearchResultDto Item, int Score)>();

        var includeCms = IncludeCms(types);
        var includeStandards = IncludeStandards(types);

        if (includeCms)
        {
            var cmsItems = await _cmsContentClient.GetPublishedContentIndexAsync(cancellationToken).ConfigureAwait(false);
            foreach (var item in cmsItems)
            {
                var score = ScoreContentItem(item.Title, item.MetaDescription, null, terms);
                if (score <= 0)
                    continue;

                string? partTitle = null;
                string? partUrl = null;
                if (!string.IsNullOrWhiteSpace(item.CollectionSlug))
                {
                    partTitle = item.CollectionTitle;
                    partUrl = "/collection/" + Uri.EscapeDataString(item.CollectionSlug.Trim());
                }

                results.Add((new SearchResultDto
                {
                    Title = item.Title,
                    Summary = item.MetaDescription,
                    Url = item.Url,
                    ContentType = item.ContentType,
                    PartOfCollectionTitle = partTitle,
                    PartOfCollectionUrl = partUrl,
                }, score));
            }
        }

        if (includeStandards && !string.IsNullOrWhiteSpace(_standardsOptions.Value.BaseUrl))
        {
            try
            {
                var standards = await FetchStandardsAsync(query, cancellationToken).ConfigureAwait(false);
                foreach (var s in standards)
                {
                    var score = ScoreContentItem(s.Title, s.Summary, null, terms);
                    if (score <= 0)
                        continue;

                    var slug = string.IsNullOrWhiteSpace(s.Slug) ? s.Id.ToString() : s.Slug.Trim();
                    results.Add((new SearchResultDto
                    {
                        Title = s.Title ?? string.Empty,
                        Summary = s.Summary,
                        Url = "/standards/" + Uri.EscapeDataString(slug),
                        ContentType = "Standard",
                    }, score));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Standards search failed for query '{Query}'", query);
            }
        }

        var filtered = types is { Count: > 0 }
            ? results.Where(r => types.Any(t => string.Equals(r.Item.ContentType, t, StringComparison.OrdinalIgnoreCase))).ToList()
            : results;

        return filtered
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Item.Title, StringComparer.OrdinalIgnoreCase)
            .Select(r => r.Item)
            .ToList();
    }

    private async Task<IReadOnlyList<StandardsListRow>> FetchStandardsAsync(string query, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(StandardsHttpClientName);
        var queryParams = new List<string>
        {
            "sort=title",
            "pagination[page]=1",
            "pagination[pageSize]=100",
            "pagination[withCount]=true",
            "populate[categories]=true",
            "populate[sub_categories]=true",
        };
        if (!string.IsNullOrWhiteSpace(query))
            queryParams.Add("filters[title][$containsi]=" + Uri.EscapeDataString(query.Trim()));

        var url = "api/standards?" + string.Join("&", queryParams);
        using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Standards CMS returned {Status} for search", (int)response.StatusCode);
            return [];
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<StandardsListRow>();
        foreach (var item in data.EnumerateArray())
        {
            var row = MapStandardRow(item);
            if (row != null)
                list.Add(row);
        }

        return list;
    }

    private static StandardsListRow? MapStandardRow(JsonElement item)
    {
        var attrs = item.TryGetProperty("attributes", out var a) ? a : item;
        var id = item.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var idVal) ? idVal : 0;
        var title = GetString(attrs, "title");
        var slug = GetString(attrs, "slug");
        var summary = GetString(attrs, "summary");
        if (string.IsNullOrEmpty(slug) && string.IsNullOrEmpty(title))
            return null;

        return new StandardsListRow(id, title ?? slug ?? "", slug ?? "", summary);
    }

    private static string? GetString(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var p))
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

    private static bool IncludeCms(IReadOnlyList<string>? types)
    {
        if (types is not { Count: > 0 })
            return true;

        var cmsTypes = new[]
        {
            "Article", "Collection", "Detailed Guide", "Detailed Guide Page", "HTML Page", "Roadmap", "Lifecycle",
            "Lifecycle Stage",
        };
        return types.Any(t => cmsTypes.Contains(t, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IncludeStandards(IReadOnlyList<string>? types)
    {
        if (types is not { Count: > 0 })
            return true;
        return types.Any(t => t.Equals("Standard", StringComparison.OrdinalIgnoreCase));
    }

    private static int ScoreContentItem(string? title, string? meta, string? body, string[] terms)
    {
        if (terms.Length == 0)
            return 0;

        var titleNorm = Normalize(title);
        var metaNorm = Normalize(meta);
        var bodyNorm = Normalize(body);
        if (string.IsNullOrEmpty(titleNorm) && string.IsNullOrEmpty(metaNorm) && string.IsNullOrEmpty(bodyNorm))
            return 0;

        var score = 0;
        var queryNorm = string.Join(" ", terms);

        foreach (var term in terms)
        {
            if (string.IsNullOrEmpty(term))
                continue;

            if (!string.IsNullOrEmpty(titleNorm) && titleNorm.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += titleNorm.Equals(term, StringComparison.OrdinalIgnoreCase) ? ScoreTitleExact : ScoreTitleContains;
            else if (!string.IsNullOrEmpty(metaNorm) && metaNorm.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += metaNorm.Equals(term, StringComparison.OrdinalIgnoreCase) ? ScoreMetaExact : ScoreMetaContains;
            else if (!string.IsNullOrEmpty(bodyNorm) && bodyNorm.Contains(term, StringComparison.OrdinalIgnoreCase))
                score += 10;
        }

        if (!string.IsNullOrEmpty(titleNorm) && titleNorm.Contains(queryNorm, StringComparison.OrdinalIgnoreCase))
            score += 20;
        if (!string.IsNullOrEmpty(metaNorm) && metaNorm.Contains(queryNorm, StringComparison.OrdinalIgnoreCase))
            score += 15;

        return score;
    }

    private static string? Normalize(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private sealed record StandardsListRow(int Id, string Title, string Slug, string? Summary);
}
