using DdtManual.Infrastructure.Standards;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DdtManual.Web.Controllers;

/// <summary>Browse and view DDT standards from Standards CMS at <c>/standards</c>.</summary>
[Route("standards")]
public sealed class StandardsController : Controller
{
    private readonly DdtStandardsApiService _apiService;
    private readonly ILogger<StandardsController> _logger;
    private readonly StandardsCmsOptions _cmsOptions;

    public StandardsController(
        DdtStandardsApiService apiService,
        ILogger<StandardsController> logger,
        IOptions<StandardsCmsOptions> cmsOptions)
    {
        _apiService = apiService;
        _logger = logger;
        _cmsOptions = cmsOptions.Value;
    }

    /// <summary>List published standards (search, category filters, pagination).</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        [FromQuery(Name = "category")] string[]? category,
        string? sortBy,
        string? sortDirection,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_cmsOptions.BaseUrl))
        {
            ViewBag.ErrorMessage = "Standards integration is not configured. Set StandardsCMS:BaseUrl.";
            return View(
                "~/Views/Standards/Index.cshtml",
                new DdtStandardsResponse { Data = [] });
        }

        try
        {
            var categoriesList = category?.Where(c => !string.IsNullOrWhiteSpace(c)).ToList() ?? [];

            const int pageSize = 10;
            var response = await _apiService.GetPublishedStandardsAsync(
                    search: search,
                    categories: categoriesList,
                    sortBy: sortBy,
                    sortDirection: sortDirection,
                    page: page,
                    pageSize: pageSize,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (response == null)
            {
                ViewBag.ErrorMessage = "Unable to load standards at this time.";
                return View(
                    "~/Views/Standards/Index.cshtml",
                    new DdtStandardsResponse { Data = [] });
            }

            ViewBag.Search = search;
            ViewBag.Category = categoriesList;
            ViewBag.SortBy = sortBy;
            ViewBag.SortDirection = sortDirection;
            ViewBag.PageSize = pageSize;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = response.Pagination?.TotalPages ?? 1;
            ViewBag.TotalRecords = response.Pagination?.TotalRecords ?? 0;

            var unfiltered = await _apiService.GetPublishedStandardsAsync(page: 1, pageSize: 500, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var dataForCategories = unfiltered?.Data is { Count: > 0 }
                ? unfiltered.Data
                : response.Data;

            var allCategories = dataForCategories
                .SelectMany(s => s.Categories ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct()
                .OrderBy(c => c!)
                .ToList();

            var categoryCounts = dataForCategories
                .SelectMany(s => (s.Categories ?? [])
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(c => c!))
                .GroupBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            ViewBag.Categories = allCategories;
            ViewBag.CategoryCounts = categoryCounts;
            ViewBag.TotalStandardsForFilter = dataForCategories.Count;

            response.Data = response.Data.OrderBy(s => s.Title).ToList();

            return View("~/Views/Standards/Index.cshtml", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading DDT standards");
            ViewBag.ErrorMessage = "An error occurred while loading standards. Please try again later.";
            return View(
                "~/Views/Standards/Index.cshtml",
                new DdtStandardsResponse { Data = [] });
        }
    }

    /// <summary>Legacy path used by the previous site: <c>/standards/ddt-standards/{slug}</c>.</summary>
    [HttpGet("ddt-standards/{slug}")]
    public Task<IActionResult> DetailsLegacyDdtStandards(string slug, CancellationToken cancellationToken)
        => Details(slug, cancellationToken);

    /// <summary>Standard detail by slug (<c>/standards/{slug}</c>).</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Details(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_cmsOptions.BaseUrl))
        {
            ViewBag.ErrorMessage = "Standards integration is not configured.";
            return View("~/Views/Standards/Details.cshtml", (DdtStandardDetailDto?)null);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                ViewBag.ErrorMessage = "Standard slug is required.";
                return View("~/Views/Standards/Details.cshtml", (DdtStandardDetailDto?)null);
            }

            var standard = await _apiService.GetStandardBySlugAsync(slug, cancellationToken).ConfigureAwait(false);

            if (standard == null)
            {
                ViewBag.ErrorMessage =
                    "Standard not found. The standard may not exist or may not be published.";
                return View("~/Views/Standards/Details.cshtml", (DdtStandardDetailDto?)null);
            }

            ViewBag.CompassStandardUrl = _apiService.GetManageStandardUrl(standard.DocumentId);

            var categoryNames = standard.Categories?
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => c.Name!)
                    .Distinct()
                    .ToList()
                ?? [];
            if (categoryNames.Count > 0)
            {
                var relatedResponse = await _apiService.GetPublishedStandardsAsync(
                        categories: categoryNames,
                        page: 1,
                        pageSize: 50,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var related = relatedResponse?.Data?
                        .Where(s => !string.Equals(s.Slug, standard.Slug, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(s => s.Title)
                        .ToList()
                    ?? [];
                ViewBag.RelatedStandards = related;
            }
            else
            {
                ViewBag.RelatedStandards = new List<DdtStandardDto>();
            }

            return View("~/Views/Standards/Details.cshtml", standard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading DDT standard with slug {Slug}", slug);
            ViewBag.ErrorMessage = "An error occurred while loading the standard. Please try again later.";
            return View("~/Views/Standards/Details.cshtml", (DdtStandardDetailDto?)null);
        }
    }
}
