using DdtManual.Web.Models.Templates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace DdtManual.Web.Controllers;

/// <summary>Sample routes to preview Razor templates in Development. Map real controllers to the same views later.</summary>
[Route("ui/templates")]
public sealed class UiTemplatesController(IWebHostEnvironment environment) : Controller
{
    private IActionResult? DenyIfNotDevelopment()
    {
        if (!environment.IsDevelopment())
            return NotFound();
        return null;
    }

    [HttpGet("collection")]
    public IActionResult Collection()
    {
        if (DenyIfNotDevelopment() is { } denied)
            return denied;

        var model = new ManualCollectionPageModel
        {
            Title = "Sample collection",
            Slug = "sample-collection",
            MetaDescription = "Illustrates collection layout: intro, grouped links, and sidebar.",
            BodyHtml = "<p class=\"govuk-body\">This is the collection introduction rendered as HTML from your application layer.</p>",
            Sections =
            [
                new ManualCollectionSectionModel
                {
                    Title = "Guidance in this collection",
                    Items =
                    [
                        new ManualCollectionLinkModel
                        {
                            Title = "Example detailed guide",
                            Url = "/guidance/guides/example",
                            ContentType = "Guidance",
                        },
                        new ManualCollectionLinkModel
                        {
                            Title = "External resource",
                            Url = "https://www.gov.uk/",
                            ContentType = "External link",
                            LinkType = "Policy",
                            OpenInNewTab = true,
                        },
                    ],
                },
            ],
            RelatedContent =
            [
                new ManualRelatedContentModel
                {
                    HeaderHtml = "Related",
                    ContentHtml = "<p class=\"govuk-body-s\">Optional inset HTML from CMS.</p>",
                },
            ],
            RelatedFiles =
            [
                new ManualRelatedFileModel
                {
                    Name = "Example PDF",
                    Url = "#",
                    FileType = "PDF",
                    SizeDisplay = "120 KB",
                    Caption = "Optional caption",
                },
            ],
        };
        return View("~/Views/Templates/Collection.cshtml", model);
    }

    [HttpGet("detailed-guide")]
    public IActionResult DetailedGuide()
    {
        if (DenyIfNotDevelopment() is { } denied)
            return denied;

        var model = new ManualDetailedGuidePageModel
        {
            IsOverviewPage = true,
            GuideSlug = "example-guide",
            HeroTitle = "Example detailed guide",
            HeroIntro = "Overview page with contents and body.",
            CollectionSlug = "sample-collection",
            CollectionTitle = "Sample collection",
            ShowContents = true,
            ContentsUseNumbers = true,
            ContentsItems =
            [
                new ManualGuideContentsItemModel { Number = 1, Title = "Overview", Url = null, IsCurrent = true },
                new ManualGuideContentsItemModel { Number = 2, Title = "Planning", Url = "/ui/templates/detailed-guide/planning", IsCurrent = false },
            ],
            PageTitle = "Overview",
            ShowPageHeader = true,
            BodyHtml = "<p class=\"govuk-body\">Main guide body HTML.</p>",
            PaginationNextUrl = "/ui/templates/detailed-guide/planning",
            PaginationNextLabel = "Planning",
            ShowGuidePagesOnRight = false,
            GuidePagesRightNav = [],
        };
        return View("~/Views/Templates/DetailedGuide.cshtml", model);
    }

    [HttpGet("detailed-guide/planning")]
    public IActionResult DetailedGuideChild()
    {
        if (DenyIfNotDevelopment() is { } denied)
            return denied;

        var model = new ManualDetailedGuidePageModel
        {
            IsOverviewPage = false,
            GuideSlug = "example-guide",
            HeroTitle = "Example detailed guide",
            PageTitle = "Planning",
            HeroIntro = "Overview page with contents and body.",
            CollectionSlug = "sample-collection",
            CollectionTitle = "Sample collection",
            ShowContents = true,
            ContentsUseNumbers = true,
            ContentsItems =
            [
                new ManualGuideContentsItemModel { Number = 1, Title = "Overview", Url = "/ui/templates/detailed-guide", IsCurrent = false },
                new ManualGuideContentsItemModel { Number = 2, Title = "Planning", Url = null, IsCurrent = true },
            ],
            ShowPageHeader = true,
            BodyHtml = "<p class=\"govuk-body\">Content for the Planning page.</p>",
            PaginationPrevUrl = "/ui/templates/detailed-guide",
            PaginationPrevLabel = "Overview",
            PaginationNextUrl = null,
            ShowGuidePagesOnRight = true,
            GuidePagesRightNav =
            [
                new ManualGuideRightNavItemModel { Number = 1, Title = "Planning", Url = null, IsCurrent = true },
            ],
        };
        return View("~/Views/Templates/DetailedGuide.cshtml", model);
    }

    [HttpGet("standards")]
    public IActionResult Standards([FromQuery] string? search, [FromQuery(Name = "category")] string[]? category)
    {
        if (DenyIfNotDevelopment() is { } denied)
            return denied;

        var all = new List<ManualStandardSummaryModel>
        {
            new()
            {
                Slug = "accessibility-conformance",
                Title = "Accessibility and inclusion",
                Summary = "Services must be usable by everyone who needs them.",
                CategoryLabel = "User-centred design",
            },
            new()
            {
                Slug = "secure-services",
                Title = "Secure services",
                Summary = "Protect user data and government systems.",
                CategoryLabel = "Technology",
            },
            new()
            {
                Slug = "architecture-decisions",
                Title = "Architecture decision records",
                Summary = "Document significant technical decisions.",
                CategoryLabel = "Architecture",
            },
        };

        var categoriesDistinct = all
            .Select(s => s.CategoryLabel)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var categoryCounts = all
            .Where(s => !string.IsNullOrEmpty(s.CategoryLabel))
            .GroupBy(s => s.CategoryLabel!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var selectedCategories = category?.Where(c => !string.IsNullOrWhiteSpace(c)).ToList() ?? [];

        IEnumerable<ManualStandardSummaryModel> filtered = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            filtered = filtered.Where(s =>
                s.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (s.Summary?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (selectedCategories.Count > 0)
        {
            filtered = filtered.Where(s =>
                s.CategoryLabel != null
                && selectedCategories.Contains(s.CategoryLabel, StringComparer.OrdinalIgnoreCase));
        }

        var model = new ManualStandardsListPageModel
        {
            PageTitle = "DDT Standards",
            Intro = "Digital, data and technology standards to support good service delivery.",
            Standards = filtered.ToList(),
            SearchQuery = search,
            Categories = categoriesDistinct,
            SelectedCategories = selectedCategories,
            CategoryCounts = categoryCounts,
            TotalStandardsCount = all.Count,
            TotalPages = 1,
            CurrentPage = 1,
        };
        return View("~/Views/Templates/StandardsIndex.cshtml", model);
    }

    [HttpGet("standards/{slug}")]
    public IActionResult StandardDetail([FromRoute] string slug)
    {
        if (DenyIfNotDevelopment() is { } denied)
            return denied;

        var templateSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "accessibility-conformance",
            "accessibility",
            "secure-services",
            "architecture-decisions",
        };
        if (!templateSlugs.Contains(slug))
            return NotFound();

        var model = new ManualStandardDetailPageModel
        {
            Title = "Accessibility and inclusion",
            Summary = "Ensure your service can be used by people with diverse needs.",
            PurposeHtml = "<p class=\"govuk-body\">Purpose content as HTML.</p>",
            HowToMeetHtml = "<ul class=\"govuk-list govuk-list--bullet\"><li>Meet WCAG 2.2 AA</li><li>Test with assistive technology</li></ul>",
            GovernanceHtml = "<p class=\"govuk-body\">Governance content.</p>",
            RelatedGuidanceHtml = "<p class=\"govuk-body\"><a href=\"#\" class=\"govuk-link\">Related guidance link</a></p>",
            Categories =
            [
                new ManualStandardCategoryModel
                {
                    Name = "User-centred design",
                    SubCategoryNames = ["Research", "Design"],
                },
            ],
            Version = "1.0",
            LastUpdated = new DateTime(2026, 3, 15),
            ListPageUrl = Url.Action(nameof(Standards), "UiTemplates") ?? "/ui/templates/standards",
            RelatedStandards =
            [
                new ManualStandardSummaryModel { Slug = "secure-services", Title = "Secure services" },
            ],
        };
        return View("~/Views/Templates/StandardDetail.cshtml", model);
    }

}
