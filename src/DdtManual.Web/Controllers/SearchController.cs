using DdtManual.Application.Abstractions;
using DdtManual.Application.Content;
using DdtManual.Web.Models.Templates;
using Microsoft.AspNetCore.Mvc;

namespace DdtManual.Web.Controllers;

[Route("search")]
public sealed class SearchController(ISearchService searchService, ILogger<SearchController> logger) : Controller
{
    /// <summary>Content types shown in Service Manual search filters (facets use labels from result set).</summary>
    public static readonly IReadOnlyList<string> AllContentTypes =
    [
        "Article",
        "Collection",
        "Detailed Guide",
        "Detailed Guide Page",
        "HTML Page",
        "Roadmap",
        "Lifecycle",
        "Lifecycle Stage",
        "Standard",
    ];

    /// <summary>GET /search or /search/all — keyword search with optional type filters.</summary>
    [HttpGet("")]
    [HttpGet("all")]
    public async Task<IActionResult> Index(
        string? keywords,
        [FromQuery(Name = "type")] List<string>? types,
        [FromQuery] string? highlight,
        CancellationToken cancellationToken)
    {
        var query = keywords?.Trim();
        var typeFilter = types?.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            ?? [];
        var showHighlight = string.Equals(highlight, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(highlight, "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(query))
        {
            return View("~/Views/Templates/SearchResults.cshtml", new ManualSearchPageModel
            {
                Keywords = "",
                Types = typeFilter,
                Facets = [],
                Results = [],
                HighlightKeywords = showHighlight,
            });
        }

        IReadOnlyList<SearchResultDto> dtoResults;
        try
        {
            dtoResults = await searchService.SearchAsync(query, typeFilter, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Search failed for query '{Query}'", query);
            return View("~/Views/Templates/SearchResults.cshtml", new ManualSearchPageModel
            {
                Keywords = query,
                Types = typeFilter,
                Facets = [],
                Results = [],
                HighlightKeywords = showHighlight,
            });
        }

        var facets = dtoResults
            .GroupBy(r => r.ContentType, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key)
            .Select(g => new ManualSearchFacetModel { ContentType = g.Key, Count = g.Count() })
            .ToList();

        var results = dtoResults.Select(MapToItem).ToList();

        return View("~/Views/Templates/SearchResults.cshtml", new ManualSearchPageModel
        {
            Keywords = query,
            Types = typeFilter,
            Facets = facets,
            Results = results,
            HighlightKeywords = showHighlight,
        });
    }

    private static ManualSearchResultItemModel MapToItem(SearchResultDto r) =>
        new()
        {
            Title = r.Title,
            Url = r.Url,
            ContentType = r.ContentType,
            Summary = r.Summary,
            PartOfCollectionTitle = r.PartOfCollectionTitle,
            PartOfCollectionUrl = r.PartOfCollectionUrl,
        };
}
